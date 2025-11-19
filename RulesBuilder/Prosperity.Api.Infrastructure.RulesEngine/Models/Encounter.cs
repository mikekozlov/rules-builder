namespace Prosperity.Api.Infrastructure.RulesEngine.Models;

public class Encounter(string noteType, int? encounterDuration, string[]? providerCredentials)
{
    private string[] _providerCredentials = providerCredentials ?? System.Array.Empty<string>();

    public string NoteType { get; set; } = noteType;
    public int? EncounterDuration { get; set; } = encounterDuration;
    public string[] ProviderCredentials
    {
        get => _providerCredentials;
        set => _providerCredentials = value ?? System.Array.Empty<string>();
    }
}

public class CptCodeSuggestion
{
    public Encounter Encounter { get; }
    public string CptCodes { get; }
    public string RuleName { get; }

    public CptCodeSuggestion(Encounter encounter, string cptCodes, string ruleName)
    {
        Encounter = encounter;
        CptCodes = cptCodes;
        RuleName = ruleName;
    }
}

public class CptCodeOutput
{
    public List<string> CptCodes { get; set; } = new();

    public CptCodeOutput()
    {
        CptCodes = new List<string>();
    }

    public CptCodeOutput(List<string> cptCodes)
    {
        CptCodes = cptCodes;
    }

    public CptCodeOutput(string cptCodes)
    {
        if (string.IsNullOrWhiteSpace(cptCodes))
        {
            CptCodes = new List<string>();
        }
        else
        {
            CptCodes = cptCodes.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(code => code.Trim())
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .ToList();
        }
    }
}
