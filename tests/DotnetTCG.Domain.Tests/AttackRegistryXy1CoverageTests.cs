using System.Text.Json;
using DotnetTCG.Domain.Attacks;

namespace DotnetTCG.Domain.Tests;

/// <summary>
/// AUDIT D011 — cobertura de AttackRegistry contra data/xy1.json (146 cartas).
/// Regla: todo ataque de xy1 con daño variable (damage "N×" / "N+") o con texto de efecto
/// debe resolver <c>Find(nombre) != null</c>, salvo los listados en <see cref="KnownGaps"/>
/// (brecha documentada, no verde silencioso). La dirección inversa (clave del registry →
/// ataque real de xy1) también se valida, para que no reaparezcan claves muertas.
/// </summary>
public sealed class AttackRegistryXy1CoverageTests
{
    private static readonly AttackRegistry Registry = new();

    /// <summary>
    /// Claves del registry que NO existen como ataque en xy1. Verificado contra las 146 cartas:
    /// ninguna carta real tiene un ataque con ese nombre. Se conservan a propósito porque los
    /// tests existentes las referencian; ninguna carta puede depender de ellas.
    /// </summary>
    private static readonly string[] DocumentedDeadKeys =
        ["Metal", "King's Smash", "X Scissor", "Rock Throw"];

    /// <summary>
    /// AUDIT D011 — brecha conocida: ataques de xy1 cuyo efecto no tiene mapeo correcto al
    /// sistema de EffectSpec actual, agrupados por motivo:
    ///  - inmunidad a Resistencia/Debilidad: el mecanismo vive en DamageCalculator
    ///    (fuera del alcance de D011; "Mist Slash" sólo cubre Debilidad por nombre);
    ///  - elección de destino del jugador: el pipeline sólo resuelve un destino de banca;
    ///  - mecanismo ausente o de granularidad incorrecta (filtro por tipo/nombre, prevención
    ///    condicional, auto-resticción por nombre de ataque);
    ///  - flip compartido o trigger diferido sin soporte.
    /// Si implementás uno, sacalo de esta lista: KnownGaps_AreRealXy1… lo exige.
    /// </summary>
    private static readonly string[] KnownGaps =
    [
        // Inmunidad a Resistencia/Debilidad (DamageCalculator fuera de alcance de D011):
        "Buster Swing",
        "Mist Slash",
        "Puncture",
        "Rock Tumble",
        // Elección de destino que el pipeline no resuelve:
        "Eerie Impulse",   // "1 de los Pokémon del rival"
        "Gather Energy",   // "1 de tus Pokémon" (incluye activo y banca)
        "Sweet Scent",     // "1 de tus Pokémon" (incluye activo)
        "Hydro Bombard",   // 2 objetivos en la banca rival
        "Tree Slam",       // 2 objetivos en la banca rival
        // Mecanismo ausente o de granularidad incorrecta:
        "Gastro Acid",     // neutraliza Abilities
        "Harden",          // prevención condicional (sólo si el daño es ≤10)
        "Dig",             // previene "todos los efectos" (sólo hay prevención de daño)
        "King's Shield",   // prevención + auto-resticción por nombre de ataque
        "Metal Wallop",    // boost de daño filtrado por nombre de ataque
        "Pheromotion",     // búsqueda filtrada por tipo de carta
        "X Blast",         // auto-resticción por nombre de ataque
        // Flip compartido / trigger diferido / flips múltiples:
        "Distortion Beam", // un único flip decide Asleep (cara) o Confused (cola)
        "Mental Panic",    // trigger en el turno rival con flip
        "Mental Trash",    // 4 flips con descarte proporcional (EffectType.FlipCoin es no-op)
    ];

    [Fact]
    public void Xy1Attacks_WithVariableDamageOrEffectText_AreRegistered()
    {
        var attacks = LoadXy1Attacks();
        Assert.True(attacks.Count > 100,
            $"data/xy1.json cargó sólo {attacks.Count} ataques: revisá la ruta/lectura");

        var missing = attacks
            .Where(a => NeedsBehavior(a) && Registry.Find(a.Name) is null
                        && !KnownGaps.Contains(a.Name))
            .Select(a => a.Name)
            .Distinct()
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0,
            "Ataques de xy1 con daño variable o texto de efecto sin behavior en AttackRegistry " +
            "(agregá el behavior o, si no tiene mapeo correcto, documentalo en KnownGaps):\n  " +
            string.Join("\n  ", missing));
    }

    [Fact]
    public void KnownGaps_AreRealXy1Attacks_AndStillUnregistered()
    {
        var names = LoadXy1Attacks().Select(a => a.Name).ToHashSet(StringComparer.Ordinal);

        var stale = KnownGaps.Where(g => !names.Contains(g)).ToList();
        Assert.True(stale.Count == 0,
            "KnownGaps con nombres que no existen en xy1 (corregí la lista): " +
            string.Join(", ", stale));

        var implemented = KnownGaps.Where(g => Registry.Find(g) is not null).ToList();
        Assert.True(implemented.Count == 0,
            "Estos ataques ya están registrados: sacalos de KnownGaps: " +
            string.Join(", ", implemented));
    }

    [Fact]
    public void KnownGaps_DoNotHideVariableDamageAttacks()
    {
        // El daño variable es el núcleo de D011: jamás puede quedar como brecha documentada.
        var variable = LoadXy1Attacks()
            .Where(a => a.Damage.Contains('×') || a.Damage.Contains('+'))
            .Select(a => a.Name)
            .ToHashSet(StringComparer.Ordinal);

        var hidden = KnownGaps.Where(variable.Contains).ToList();
        Assert.True(hidden.Count == 0,
            "Un ataque con daño variable no puede estar en KnownGaps: " +
            string.Join(", ", hidden));
    }

    [Fact]
    public void RegistryKeys_MatchXy1_OrAreDocumentedDeadKeys()
    {
        var names = LoadXy1Attacks().Select(a => a.Name).ToHashSet(StringComparer.Ordinal);
        var registered = new AttackRegistry().RegisteredNames;

        var invalid = registered
            .Where(k => !names.Contains(k) && !DocumentedDeadKeys.Contains(k))
            .ToList();
        Assert.True(invalid.Count == 0,
            "Claves del registry que no son ataques reales de xy1 (renombralas o, si ninguna " +
            "carta los usa, documentalas en DocumentedDeadKeys): " + string.Join(", ", invalid));

        var deadNotRegistered = DocumentedDeadKeys.Where(k => !registered.Contains(k)).ToList();
        Assert.True(deadNotRegistered.Count == 0,
            "DocumentedDeadKeys ya no están registradas (sacalas de la lista): " +
            string.Join(", ", deadNotRegistered));
    }

    private sealed record Xy1Attack(string Name, string Damage, string Text);

    private static bool NeedsBehavior(Xy1Attack attack)
        => attack.Damage.Contains('×')
           || attack.Damage.Contains('+')
           || !string.IsNullOrWhiteSpace(attack.Text);

    private static List<Xy1Attack> LoadXy1Attacks()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(FindXy1DataPath()));
        var attacks = new List<Xy1Attack>();
        foreach (var card in doc.RootElement.GetProperty("data").EnumerateArray())
        {
            if (!card.TryGetProperty("attacks", out var list))
                continue;
            foreach (var atk in list.EnumerateArray())
            {
                attacks.Add(new Xy1Attack(
                    atk.GetProperty("name").GetString() ?? string.Empty,
                    atk.TryGetProperty("damage", out var damage)
                        ? damage.GetString() ?? string.Empty : string.Empty,
                    atk.TryGetProperty("text", out var text)
                        ? text.GetString() ?? string.Empty : string.Empty));
            }
        }

        return attacks;
    }

    /// <summary>Sube directorios desde AppContext.BaseDirectory hasta encontrar data/xy1.json.</summary>
    private static string FindXy1DataPath()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "data", "xy1.json");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            $"No se encontró data/xy1.json subiendo desde {AppContext.BaseDirectory}");
    }
}
