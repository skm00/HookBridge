import type { JSX } from 'react';

type IconProps = {
  name: 'bolt' | 'shield' | 'chart' | 'retry' | 'docs' | 'check' | 'menu' | 'bell' | 'key' | 'server' | 'code' | 'spark';
  className?: string;
};

const paths: Record<IconProps['name'], JSX.Element> = {
  bolt: <path d="M11 2 4 12h6l-1 8 7-11h-6l1-7Z" stroke="currentColor" strokeWidth="1.8" strokeLinejoin="round" />,
  shield: <path d="M10 2.5 4 5v5.2c0 3.7 2.4 6.7 6 7.8 3.6-1.1 6-4.1 6-7.8V5l-6-2.5Z" stroke="currentColor" strokeWidth="1.7" strokeLinejoin="round" />,
  chart: <path d="M4 15.5V5m4 10.5v-7m4 7v-10m4 10v-4" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />,
  retry: <path d="M15.5 6.5A6 6 0 1 0 16 14M15.5 6.5V3.5M15.5 6.5h-3" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />,
  docs: <path d="M6 3.5h5l3 3V16.5H6V3.5Z M11 3.5V7h3 M8 10h4 M8 13h4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />,
  check: <path d="m4 10 4 4 8-8" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />,
  menu: <path d="M3.5 6h13M3.5 10h13M3.5 14h13" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />,
  bell: <path d="M7 15a3 3 0 0 0 6 0M5 13h10l-1.2-1.6V8a3.8 3.8 0 0 0-7.6 0v3.4L5 13Z" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />,
  key: <path d="M8.5 10.5a3.5 3.5 0 1 1 2.7 3.4L9 16H7v-2H5v-2h2.1l1.4-1.5Z M13 7h.01" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />,
  server: <path d="M4 5.5h12v4H4v-4ZM4 10.5h12v4H4v-4ZM7 7.5h.01M7 12.5h.01" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />,
  code: <path d="m8 6-4 4 4 4M12 6l4 4-4 4" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />,
  spark: <path d="M10 2.5 11.8 8l5.7 2-5.7 2L10 17.5 8.2 12 2.5 10l5.7-2L10 2.5Z" stroke="currentColor" strokeWidth="1.5" strokeLinejoin="round" />
};

const Icon = ({ name, className = 'h-5 w-5' }: IconProps): JSX.Element => (
  <svg viewBox="0 0 20 20" fill="none" aria-hidden="true" className={className}>
    {paths[name]}
  </svg>
);

export default Icon;
