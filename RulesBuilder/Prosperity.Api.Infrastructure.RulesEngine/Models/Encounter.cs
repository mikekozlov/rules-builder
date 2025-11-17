namespace Prosperity.Api.Infrastructure.RulesEngine.Models;

public class Encounter
{
    public string NoteType { get; set; } = string.Empty;
    public int EncounterDuration { get; set; }
    public string ProviderCredentials { get; set; } = string.Empty;

    public Encounter(string noteType, int encounterDuration, string providerCredentials)
    {
        NoteType = noteType;
        EncounterDuration = encounterDuration;
        ProviderCredentials = providerCredentials;
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
