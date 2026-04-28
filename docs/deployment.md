# HookBridge Deployment (Ubuntu 22.04, 1GB RAM)

## 0) Set variables

```bash
export PUBLIC_IP="<YOUR_PUBLIC_IP>"
export REPO_URL="https://github.com/<your-org-or-user>/HookBridge.git"
export APP_ROOT="/opt/hookbridge"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
```

## 1) Base packages

```bash
sudo apt update
sudo apt install -y git curl ca-certificates gnupg lsb-release unzip
```

## 2) Install .NET 8 SDK

```bash
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O /tmp/packages-microsoft-prod.deb
sudo dpkg -i /tmp/packages-microsoft-prod.deb
sudo apt update
sudo apt install -y dotnet-sdk-8.0

dotnet --info
```

## 3) Optional: install Docker (skip if running everything native)

```bash
sudo apt install -y docker.io docker-compose-plugin
sudo systemctl enable --now docker
sudo usermod -aG docker $USER
```

Log out/login after `usermod` if you want Docker without `sudo`.

## 4) Add swap (recommended on 1GB RAM)

```bash
sudo fallocate -l 2G /swapfile || sudo dd if=/dev/zero of=/swapfile bs=1M count=2048
sudo chmod 600 /swapfile
sudo mkswap /swapfile
sudo swapon /swapfile

# Persist across reboot
if ! grep -q '^/swapfile ' /etc/fstab; then echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab; fi

# Lower swap aggressiveness
echo 'vm.swappiness=10' | sudo tee /etc/sysctl.d/99-swappiness.conf
sudo sysctl --system
free -h
```

## 5) Clone repo and publish binaries (Release)

```bash
sudo mkdir -p $APP_ROOT
sudo chown -R $USER:$USER $APP_ROOT

git clone $REPO_URL $APP_ROOT
cd $APP_ROOT

dotnet restore

dotnet publish src/HookBridge.Api/HookBridge.Api.csproj -c Release -o $APP_ROOT/publish/api

dotnet publish src/HookBridge.Worker/HookBridge.Worker.csproj -c Release -o $APP_ROOT/publish/worker
```

## 6) Lightweight Kafka (single-node KRaft, no ZooKeeper)

```bash
cd /opt
sudo curl -L -o kafka.tgz https://downloads.apache.org/kafka/3.8.1/kafka_2.13-3.8.1.tgz
sudo tar -xzf kafka.tgz
sudo mv kafka_2.13-3.8.1 kafka
sudo mkdir -p /var/lib/kafka-kraft
sudo chown -R $USER:$USER /opt/kafka /var/lib/kafka-kraft
```

Create minimal Kafka config:

```bash
cat > /opt/kafka/config/kraft/server.properties <<EOF_KAFKA
process.roles=broker,controller
node.id=1
controller.quorum.voters=1@127.0.0.1:9093
listeners=PLAINTEXT://0.0.0.0:9092,CONTROLLER://127.0.0.1:9093
advertised.listeners=PLAINTEXT://${PUBLIC_IP}:9092
listener.security.protocol.map=CONTROLLER:PLAINTEXT,PLAINTEXT:PLAINTEXT
controller.listener.names=CONTROLLER
inter.broker.listener.name=PLAINTEXT
num.partitions=1
offsets.topic.replication.factor=1
transaction.state.log.replication.factor=1
transaction.state.log.min.isr=1
default.replication.factor=1
min.insync.replicas=1
log.dirs=/var/lib/kafka-kraft
log.retention.hours=24
log.segment.bytes=67108864
num.network.threads=2
num.io.threads=2
socket.send.buffer.bytes=102400
socket.receive.buffer.bytes=102400
socket.request.max.bytes=104857600
group.initial.rebalance.delay.ms=0
EOF_KAFKA
```

Format KRaft storage once:

```bash
KAFKA_CLUSTER_ID=$(/opt/kafka/bin/kafka-storage.sh random-uuid)
/opt/kafka/bin/kafka-storage.sh format -t "$KAFKA_CLUSTER_ID" -c /opt/kafka/config/kraft/server.properties
```

## 7) Force low memory for Kafka

```bash
cat > /opt/kafka/bin/kafka-server-start-lowmem.sh <<'EOF_KMEM'
#!/usr/bin/env bash
export KAFKA_HEAP_OPTS="-Xms128m -Xmx256m"
exec /opt/kafka/bin/kafka-server-start.sh /opt/kafka/config/kraft/server.properties
EOF_KMEM
chmod +x /opt/kafka/bin/kafka-server-start-lowmem.sh
```

## 8) App environment files

API env:

```bash
sudo mkdir -p /etc/hookbridge

cat | sudo tee /etc/hookbridge/api.env >/dev/null <<EOF_API
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://0.0.0.0:5000
Kafka__BootstrapServers=${PUBLIC_IP}:9092
MongoDb__ConnectionString=mongodb://127.0.0.1:27017
MongoDb__DatabaseName=hookbridge
Jwt__Issuer=HookBridge
Jwt__Audience=HookBridgeUsers
Jwt__Secret=replace_with_a_very_long_random_secret_at_least_32_chars
Jwt__ExpiryMinutes=60
Encryption__MasterKey=replace_with_a_very_long_random_key_at_least_32_chars
Stripe__SecretKey=dummy
Stripe__WebhookSecret=dummy
Stripe__StarterPriceId=dummy
Stripe__ProPriceId=dummy
Stripe__SuccessUrl=http://${PUBLIC_IP}:5000/success
Stripe__CancelUrl=http://${PUBLIC_IP}:5000/cancel
EOF_API
```

Worker env:

```bash
cat | sudo tee /etc/hookbridge/worker.env >/dev/null <<EOF_WORKER
DOTNET_ENVIRONMENT=Production
Kafka__BootstrapServers=${PUBLIC_IP}:9092
Kafka__ConsumerGroupId=hookbridge-worker-group
MongoDb__ConnectionString=mongodb://127.0.0.1:27017
MongoDb__DatabaseName=hookbridge
EOF_WORKER
```

## 9) systemd services (auto-restart)

Kafka service:

```bash
cat | sudo tee /etc/systemd/system/kafka.service >/dev/null <<'EOF_KSVC'
[Unit]
Description=Apache Kafka (KRaft low memory)
After=network.target

[Service]
Type=simple
User=ubuntu
ExecStart=/opt/kafka/bin/kafka-server-start-lowmem.sh
Restart=always
RestartSec=5
LimitNOFILE=65536

[Install]
WantedBy=multi-user.target
EOF_KSVC
```

HookBridge API service:

```bash
cat | sudo tee /etc/systemd/system/hookbridge-api.service >/dev/null <<'EOF_APISVC'
[Unit]
Description=HookBridge API
After=network.target kafka.service
Requires=kafka.service

[Service]
Type=simple
User=ubuntu
WorkingDirectory=/opt/hookbridge/publish/api
EnvironmentFile=/etc/hookbridge/api.env
ExecStart=/usr/bin/dotnet /opt/hookbridge/publish/api/HookBridge.Api.dll
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
EOF_APISVC
```

HookBridge Worker service:

```bash
cat | sudo tee /etc/systemd/system/hookbridge-worker.service >/dev/null <<'EOF_WSV'
[Unit]
Description=HookBridge Worker
After=network.target kafka.service
Requires=kafka.service

[Service]
Type=simple
User=ubuntu
WorkingDirectory=/opt/hookbridge/publish/worker
EnvironmentFile=/etc/hookbridge/worker.env
ExecStart=/usr/bin/dotnet /opt/hookbridge/publish/worker/HookBridge.Worker.dll
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
EOF_WSV
```

Enable/start all:

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now kafka hookbridge-api hookbridge-worker

sudo systemctl status kafka --no-pager
sudo systemctl status hookbridge-api --no-pager
sudo systemctl status hookbridge-worker --no-pager
```

## 10) Verify public API + Kafka listener

```bash
curl -i http://${PUBLIC_IP}:5000/health
ss -tulpen | rg '5000|9092'
```

## 11) Kafka topic + produce + consume test

Create topic:

```bash
/opt/kafka/bin/kafka-topics.sh --bootstrap-server ${PUBLIC_IP}:9092 --create --if-not-exists --topic hookbridge.events --partitions 1 --replication-factor 1
```

Produce message:

```bash
echo '{"eventType":"test","payload":"hello from vm"}' | /opt/kafka/bin/kafka-console-producer.sh --bootstrap-server ${PUBLIC_IP}:9092 --topic hookbridge.events
```

Consume message:

```bash
/opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server ${PUBLIC_IP}:9092 --topic hookbridge.events --from-beginning --max-messages 1
```

## 12) Update/redeploy flow

```bash
cd /opt/hookbridge
git pull

dotnet publish src/HookBridge.Api/HookBridge.Api.csproj -c Release -o /opt/hookbridge/publish/api
dotnet publish src/HookBridge.Worker/HookBridge.Worker.csproj -c Release -o /opt/hookbridge/publish/worker

sudo systemctl restart hookbridge-api hookbridge-worker
```

## 13) Fast troubleshooting

### Kafka not starting

```bash
sudo journalctl -u kafka -n 200 --no-pager
cat /opt/kafka/logs/server.log | tail -n 100
```

Checks:

```bash
# Port conflict
ss -tulpen | rg 9092

# KRaft metadata exists
ls -lah /var/lib/kafka-kraft

# Re-format only if this is a fresh test broker with disposable data
# rm -rf /var/lib/kafka-kraft/*
# KAFKA_CLUSTER_ID=$(/opt/kafka/bin/kafka-storage.sh random-uuid)
# /opt/kafka/bin/kafka-storage.sh format -t "$KAFKA_CLUSTER_ID" -c /opt/kafka/config/kraft/server.properties
```

### Port not reachable

```bash
# Local binding
ss -tulpen | rg '5000|9092'

# Ubuntu firewall (if enabled)
sudo ufw status
sudo ufw allow 5000/tcp
sudo ufw allow 9092/tcp

# Oracle Cloud: ensure Security List / NSG allows ingress 5000 and 9092
```

### Out of memory issues

```bash
free -h
sudo dmesg -T | rg -i 'killed process|out of memory|oom'
sudo journalctl -u kafka -u hookbridge-api -u hookbridge-worker -n 200 --no-pager
```

Emergency memory reduction:

```bash
# Lower Kafka heap further (if unstable, move back to 256m max)
sudo sed -i 's/-Xmx256m/-Xmx192m/' /opt/kafka/bin/kafka-server-start-lowmem.sh
sudo systemctl restart kafka

# Keep API/Worker in server GC default; if still tight, cap dotnet GC heap per service (optional):
# Add in /etc/hookbridge/api.env and worker.env:
# DOTNET_GCHeapHardLimit=268435456
# Then restart services.
```
