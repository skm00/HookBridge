using FluentAssertions;
using HookBridge.Application.DTOs.Auth;
using HookBridge.Domain.Configuration;
using HookBridge.Domain.Entities;
using HookBridge.Domain.Enums;
using HookBridge.Infrastructure.Configuration;
using HookBridge.Shared.Api;

namespace HookBridge.Application.Tests;

public sealed class ModelPropertyCoverageTests
{
    [Fact]
    public void GetMonthlyLimit_WhenBillingPlanIsKnown_ShouldReturnConfiguredLimit()
    {
        BillingPlanLimits.GetMonthlyLimit(BillingPlan.Free).Should().Be(BillingPlanLimits.Free);
        BillingPlanLimits.GetMonthlyLimit(BillingPlan.Starter).Should().Be(BillingPlanLimits.Starter);
        BillingPlanLimits.GetMonthlyLimit(BillingPlan.Pro).Should().Be(BillingPlanLimits.Pro);
        BillingPlanLimits.GetMonthlyLimit(BillingPlan.Enterprise).Should().Be(BillingPlanLimits.Enterprise);
        BillingPlanLimits.GetMonthlyLimit((BillingPlan)999).Should().Be(BillingPlanLimits.Free);
    }

    [Fact]
    public void RoundTripProperties_WhenApplicationDomainInfrastructureAndSharedModelsAreInstantiated_ShouldPreserveAssignedValues()
    {
        var modelTypes = new[]
        {
            typeof(LoginRequestDto).Assembly,
            typeof(Tenant).Assembly,
            typeof(KafkaSettings).Assembly,
            typeof(ApiErrorResponse).Assembly,
        }
        .SelectMany(assembly => assembly.GetTypes())
        .Where(type => type is { IsClass: true, IsAbstract: false })
        .Where(type => type.GetConstructor(Type.EmptyTypes) is not null)
        .Where(type => !type.ContainsGenericParameters)
        .Where(type => type.Namespace is not null &&
            (type.Namespace.StartsWith("HookBridge.Application.DTOs", StringComparison.Ordinal) ||
             type.Namespace.StartsWith("HookBridge.Domain.Entities", StringComparison.Ordinal) ||
             type.Namespace.StartsWith("HookBridge.Infrastructure.Configuration", StringComparison.Ordinal) ||
             type.Namespace.Equals("HookBridge.Shared.Api", StringComparison.Ordinal)))
        .OrderBy(type => type.FullName)
        .ToList();

        modelTypes.Should().NotBeEmpty();

        foreach (var modelType in modelTypes)
        {
            var instance = Activator.CreateInstance(modelType);
            instance.Should().NotBeNull($"{modelType.FullName} should be constructible for serialization and binding");

            foreach (var property in modelType.GetProperties().Where(property => property.GetIndexParameters().Length == 0))
            {
                if (property.SetMethod is not null && property.SetMethod.IsPublic)
                {
                    var expectedValue = CreateValue(property.PropertyType, property.Name);
                    property.SetValue(instance, expectedValue);

                    var actualValue = property.GetValue(instance);
                    actualValue.Should().BeEquivalentTo(expectedValue, $"{modelType.Name}.{property.Name} should round-trip assigned values");
                    continue;
                }

                property.GetValue(instance).Should().NotBeNull($"{modelType.Name}.{property.Name} getter should be safe for default instances");
            }
        }
    }

    private static object? CreateValue(Type propertyType, string propertyName)
    {
        var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (targetType == typeof(string))
        {
            return $"{propertyName}-value";
        }

        if (targetType == typeof(int))
        {
            return 42;
        }

        if (targetType == typeof(long))
        {
            return 42L;
        }

        if (targetType == typeof(double))
        {
            return 42.5d;
        }

        if (targetType == typeof(bool))
        {
            return true;
        }

        if (targetType == typeof(DateTime))
        {
            return new DateTime(2026, 5, 7, 12, 0, 0, DateTimeKind.Utc);
        }

        if (targetType == typeof(DateTimeOffset))
        {
            return new DateTimeOffset(2026, 5, 7, 12, 0, 0, TimeSpan.Zero);
        }

        if (targetType.IsEnum)
        {
            var values = Enum.GetValues(targetType);
            return values.Length > 1 ? values.GetValue(1) : values.GetValue(0);
        }

        if (targetType == typeof(object))
        {
            return new Dictionary<string, object?> { ["value"] = propertyName };
        }

        if (targetType == typeof(Dictionary<string, string>))
        {
            return new Dictionary<string, string> { ["x-test"] = propertyName };
        }

        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
        {
            var list = (System.Collections.IList)Activator.CreateInstance(targetType)!;
            list.Add(CreateValue(targetType.GetGenericArguments()[0], propertyName));
            return list;
        }

        if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
        {
            var itemType = targetType.GetGenericArguments()[0];
            var listType = typeof(List<>).MakeGenericType(itemType);
            var list = (System.Collections.IList)Activator.CreateInstance(listType)!;
            list.Add(CreateValue(itemType, propertyName));
            return list;
        }

        if (targetType.GetConstructor(Type.EmptyTypes) is not null)
        {
            return Activator.CreateInstance(targetType);
        }

        return null;
    }
}
