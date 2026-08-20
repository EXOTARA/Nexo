using Nexo.Core.Hardware;

namespace Nexo.Core.AdaptiveEngine;

public static class AdaptiveEnginePolicy
{
    private static readonly EngineCategory[] CategoriesInOrder =
    {
        EngineCategory.SpeechToText,
        EngineCategory.WakeWord,
        EngineCategory.TextToSpeech,
        EngineCategory.LocalLanguageModel
    };

    public static AdaptiveEnginePlan Evaluate(
        HardwareCapabilityProfile hardwareProfile,
        HardwarePerformanceMode mode,
        IReadOnlyList<EngineDescriptor> descriptors,
        IReadOnlyList<EngineRuntimeState> runtimeStates,
        DateTimeOffset evaluatedAt)
    {
        ArgumentNullException.ThrowIfNull(hardwareProfile);
        ArgumentNullException.ThrowIfNull(descriptors);
        ArgumentNullException.ThrowIfNull(runtimeStates);

        var stateById = new Dictionary<EngineIdentifier, EngineRuntimeState>();
        foreach (var state in runtimeStates)
        {
            stateById[state.EngineId] = state;
        }

        var recommendations = new List<EngineRecommendation>(CategoriesInOrder.Length);
        foreach (var category in CategoriesInOrder)
        {
            var categoryDescriptors = descriptors
                .Where(d => d.Category == category)
                .OrderBy(d => d.Id.Value, StringComparer.Ordinal)
                .ToList();

            recommendations.Add(BuildRecommendation(
                category,
                categoryDescriptors,
                stateById,
                hardwareProfile.Tier,
                mode,
                hardwareProfile.OverallConfidence));
        }

        var generalWarnings = new List<string>();
        if (hardwareProfile.OverallConfidence != HardwareDataConfidence.Known)
        {
            generalWarnings.Add(
                "La información de hardware es parcial; las recomendaciones son conservadoras.");
        }

        var (availableTodayChanges, futureChanges) = BuildChangeLists(descriptors, recommendations);

        var summary = $"Modo {DescribeMode(mode)} · {DescribeTier(hardwareProfile.Tier)}.";

        return new AdaptiveEnginePlan(
            mode,
            hardwareProfile.Tier,
            hardwareProfile.OverallConfidence,
            summary,
            recommendations,
            generalWarnings,
            availableTodayChanges,
            futureChanges,
            evaluatedAt);
    }

    private static EngineRecommendation BuildRecommendation(
        EngineCategory category,
        IReadOnlyList<EngineDescriptor> categoryDescriptors,
        IReadOnlyDictionary<EngineIdentifier, EngineRuntimeState> stateById,
        HardwareCapabilityTier tier,
        HardwarePerformanceMode mode,
        HardwareDataConfidence confidence)
    {
        var reasons = new List<string>();
        var warnings = new List<string>();

        if (categoryDescriptors.Count == 0)
        {
            warnings.Add($"No hay motores registrados para {DescribeCategory(category)}.");
            return new EngineRecommendation(
                category,
                null,
                null,
                null,
                Array.Empty<EngineCompatibility>(),
                Array.Empty<EngineCompatibility>(),
                reasons,
                warnings,
                IsRecommendationOnly: true);
        }

        var compatibilities = categoryDescriptors
            .Select(descriptor => EvaluateCompatibility(descriptor, tier))
            .ToList();
        var compatibilityById = compatibilities.ToDictionary(c => c.EngineId);

        var compatibleDescriptors = categoryDescriptors
            .Where(d => compatibilityById[d.Id].IsCompatible)
            .ToList();

        var recommended = SelectRecommended(compatibleDescriptors, mode, confidence);
        var recommendedId = recommended?.Id;

        var configuredDescriptor = categoryDescriptors.FirstOrDefault(d =>
            stateById.TryGetValue(d.Id, out var state) && state.IsConfigured == true);
        var configuredId = configuredDescriptor?.Id;

        var activeDescriptor = categoryDescriptors.FirstOrDefault(d =>
            stateById.TryGetValue(d.Id, out var state) && state.IsActive == true);
        var activeId = activeDescriptor?.Id;

        var alternatives = compatibleDescriptors
            .Where(d => d.Id != recommendedId)
            .Select(d => compatibilityById[d.Id])
            .ToList();

        var incompatible = compatibilities
            .Where(c => !c.IsCompatible)
            .ToList();

        if (recommended is null)
        {
            warnings.Add(
                $"Ningún motor compatible disponible para {DescribeCategory(category)} con el hardware detectado.");
        }
        else
        {
            reasons.Add(
                $"{recommended.DisplayName} es la recomendación para {DescribeCategory(category)} en modo {DescribeMode(mode)}.");

            if (stateById.TryGetValue(recommended.Id, out var recommendedState))
            {
                if (recommendedState.IsAvailable != true)
                {
                    warnings.Add($"{recommended.DisplayName} todavía no está disponible en este equipo.");
                }
            }
            else
            {
                warnings.Add($"No hay información de disponibilidad para {recommended.DisplayName}.");
            }

            if (recommended.RequiresDownload)
            {
                warnings.Add($"{recommended.DisplayName} requiere descarga.");
            }

            if (recommended.RequiresRestart)
            {
                warnings.Add($"{recommended.DisplayName} requiere reiniciar Sakura para aplicarse.");
            }
        }

        var isRecommendationOnly = recommendedId is null || activeId != recommendedId;

        return new EngineRecommendation(
            category,
            configuredId,
            activeId,
            recommendedId,
            alternatives,
            incompatible,
            reasons,
            warnings,
            isRecommendationOnly);
    }

    private static EngineDescriptor? SelectRecommended(
        IReadOnlyList<EngineDescriptor> compatibleDescriptors,
        HardwarePerformanceMode mode,
        HardwareDataConfidence confidence)
    {
        if (compatibleDescriptors.Count == 0)
        {
            return null;
        }

        var useConservative = mode == HardwarePerformanceMode.Eco ||
            (mode == HardwarePerformanceMode.Automatic && confidence == HardwareDataConfidence.Unknown);

        if (useConservative)
        {
            return compatibleDescriptors
                .OrderBy(ResourceScore)
                .ThenByDescending(d => d.IncludedWithSakura)
                .ThenBy(d => d.RequiresDownload)
                .ThenBy(d => d.Id.Value, StringComparer.Ordinal)
                .First();
        }

        if (mode == HardwarePerformanceMode.Maximum)
        {
            return compatibleDescriptors
                .OrderByDescending(ResourceScore)
                .ThenByDescending(d => d.IsLocal == true)
                .ThenByDescending(d => d.IncludedWithSakura)
                .ThenBy(d => d.Id.Value, StringComparer.Ordinal)
                .First();
        }

        const int BalancedTarget = 8;
        return compatibleDescriptors
            .OrderBy(d => Math.Abs(ResourceScore(d) - BalancedTarget))
            .ThenByDescending(d => d.IncludedWithSakura)
            .ThenBy(d => d.Id.Value, StringComparer.Ordinal)
            .First();
    }

    private static EngineCompatibility EvaluateCompatibility(EngineDescriptor descriptor, HardwareCapabilityTier tier)
    {
        var maxAffordable = AffordableMaxCost(tier);
        var minimumGaps = FindGaps(descriptor.MinimumRequirement, maxAffordable);
        var recommendedGaps = FindGaps(descriptor.RecommendedRequirement, maxAffordable);

        var isCompatible = minimumGaps.Count == 0;
        var reasons = new List<string>();

        if (!isCompatible)
        {
            reasons.Add(
                $"{descriptor.DisplayName} necesita más de lo disponible: {string.Join(", ", minimumGaps)}.");
        }
        else if (recommendedGaps.Count > 0)
        {
            reasons.Add(
                $"{descriptor.DisplayName} cumple el mínimo, pero el nivel recomendado pide más: {string.Join(", ", recommendedGaps)}.");
        }
        else
        {
            reasons.Add($"{descriptor.DisplayName} cumple los requisitos recomendados para este nivel de hardware.");
        }

        return new EngineCompatibility(descriptor.Id, isCompatible, reasons);
    }

    private static List<string> FindGaps(EngineRequirement requirement, EngineCostLevel maxAffordable)
    {
        var gaps = new List<string>();
        if (ExceedsBudget(requirement.CpuCost, maxAffordable)) gaps.Add("CPU");
        if (ExceedsBudget(requirement.RamCost, maxAffordable)) gaps.Add("RAM");
        if (ExceedsBudget(requirement.GpuCost, maxAffordable)) gaps.Add("GPU");
        if (ExceedsBudget(requirement.EnergyCost, maxAffordable)) gaps.Add("energía");
        return gaps;
    }

    private static bool ExceedsBudget(EngineCostLevel cost, EngineCostLevel maxAffordable) =>
        cost != EngineCostLevel.Unknown && cost > maxAffordable;

    private static EngineCostLevel AffordableMaxCost(HardwareCapabilityTier tier) => tier switch
    {
        HardwareCapabilityTier.Basic => EngineCostLevel.Low,
        HardwareCapabilityTier.Standard => EngineCostLevel.Moderate,
        HardwareCapabilityTier.Accelerated => EngineCostLevel.High,
        HardwareCapabilityTier.HighPerformance => EngineCostLevel.High,
        _ => EngineCostLevel.Low
    };

    private static int ResourceScore(EngineDescriptor descriptor) =>
        CostScore(descriptor.RecommendedRequirement.CpuCost) +
        CostScore(descriptor.RecommendedRequirement.RamCost) +
        CostScore(descriptor.RecommendedRequirement.GpuCost) +
        CostScore(descriptor.RecommendedRequirement.EnergyCost);

    private static int CostScore(EngineCostLevel level) => level switch
    {
        EngineCostLevel.Unknown => 2,
        EngineCostLevel.Low => 1,
        EngineCostLevel.Moderate => 2,
        EngineCostLevel.High => 3,
        _ => 2
    };

    private static (IReadOnlyList<string> Today, IReadOnlyList<string> Future) BuildChangeLists(
        IReadOnlyList<EngineDescriptor> descriptors,
        IReadOnlyList<EngineRecommendation> recommendations)
    {
        var today = new List<string>();
        var future = new List<string>();

        foreach (var recommendation in recommendations)
        {
            if (recommendation.RecommendedEngineId is not { } recommendedId ||
                recommendedId == recommendation.ConfiguredEngineId)
            {
                continue;
            }

            var descriptor = descriptors.First(d => d.Id == recommendedId);
            var description = $"Cambiar {DescribeCategory(recommendation.Category)} a {descriptor.DisplayName}";

            if (descriptor.AllowsRuntimeSelection && !descriptor.RequiresRestart && !descriptor.RequiresDownload)
            {
                today.Add($"{description} ya es posible hoy sin reiniciar.");
            }
            else
            {
                var needs = new List<string>();
                if (descriptor.RequiresDownload) needs.Add("descarga");
                if (descriptor.RequiresRestart) needs.Add("reinicio");
                if (!descriptor.AllowsRuntimeSelection) needs.Add("implementación futura de selección en tiempo de ejecución");
                future.Add($"{description} requiere: {string.Join(", ", needs)}.");
            }
        }

        return (today, future);
    }

    private static string DescribeMode(HardwarePerformanceMode mode) => mode switch
    {
        HardwarePerformanceMode.Automatic => "Automático",
        HardwarePerformanceMode.Eco => "Ahorro",
        HardwarePerformanceMode.Balanced => "Equilibrado",
        HardwarePerformanceMode.Maximum => "Máximo",
        _ => "Automático"
    };

    private static string DescribeTier(HardwareCapabilityTier tier) => tier switch
    {
        HardwareCapabilityTier.Basic => "equipo con capacidad básica",
        HardwareCapabilityTier.Standard => "equipo con capacidad estándar",
        HardwareCapabilityTier.Accelerated => "equipo con capacidad acelerada",
        HardwareCapabilityTier.HighPerformance => "equipo de alto rendimiento",
        _ => "equipo con capacidad estándar"
    };

    private static string DescribeCategory(EngineCategory category) => category switch
    {
        EngineCategory.SpeechToText => "reconocimiento de voz",
        EngineCategory.WakeWord => "palabra de activación",
        EngineCategory.TextToSpeech => "síntesis de voz",
        EngineCategory.LocalLanguageModel => "modelo de lenguaje",
        _ => category.ToString()
    };
}
