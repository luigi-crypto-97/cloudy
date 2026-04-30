using System.Text.Json;
using FriendMap.Api.Contracts;

namespace FriendMap.Api.Services;

public class AdminOpsStateService
{
    private readonly object _gate = new();
    private readonly string _statePath;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private bool _demoSignalsEnabled;
    private bool _testUsersEnabled = true;
    private readonly List<AdminAdventureDto> _adventures = [];

    public AdminOpsStateService(IConfiguration configuration, IHostEnvironment environment)
    {
        var stateDir = Path.Combine(environment.ContentRootPath, "App_Data");
        _statePath = Path.Combine(stateDir, "admin-ops-state.json");

        var defaultDemoSignals = environment.IsDevelopment() ||
            string.Equals(configuration["Cloudy:DemoSignals"] ?? configuration["Cloudy__DemoSignals"], "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Environment.GetEnvironmentVariable("Cloudy__DemoSignals"), "true", StringComparison.OrdinalIgnoreCase);

        if (!TryLoadState(defaultDemoSignals))
        {
            _demoSignalsEnabled = defaultDemoSignals;
            _testUsersEnabled = true;
            _adventures.AddRange(DefaultAdventures());
            SaveState();
        }
    }

    public bool DemoSignalsEnabled
    {
        get { lock (_gate) return _demoSignalsEnabled; }
    }

    public bool TestUsersEnabled
    {
        get { lock (_gate) return _testUsersEnabled; }
    }

    public AdminFeatureFlagsDto GetFlags()
    {
        lock (_gate)
        {
            return new AdminFeatureFlagsDto(_demoSignalsEnabled, _testUsersEnabled);
        }
    }

    public AdminFeatureFlagsDto UpdateFlags(AdminFeatureFlagsUpdateRequest request)
    {
        lock (_gate)
        {
            if (request.DemoSignalsEnabled is not null) _demoSignalsEnabled = request.DemoSignalsEnabled.Value;
            if (request.TestUsersEnabled is not null) _testUsersEnabled = request.TestUsersEnabled.Value;
            SaveState();
            return new AdminFeatureFlagsDto(_demoSignalsEnabled, _testUsersEnabled);
        }
    }

    public IReadOnlyList<AdminAdventureDto> GetAdventures()
    {
        lock (_gate)
        {
            return _adventures
                .OrderByDescending(x => x.IsActive)
                .ThenBy(x => x.Title)
                .ToList();
        }
    }

    public AdminAdventureDto CreateAdventure(AdminAdventureUpsertRequest request)
    {
        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            var objectives = NormalizeObjectives(request.Objectives);
            var adventure = new AdminAdventureDto(
                Guid.NewGuid(),
                Normalize(request.Title, "Nuova avventura"),
                Normalize(request.Description, "Obiettivi sociali per attivare la zona."),
                Normalize(request.Scope, "weekly"),
                request.IsActive,
                objectives.Sum(x => x.RewardPoints),
                now,
                now,
                objectives);
            _adventures.Add(adventure);
            SaveState();
            return adventure;
        }
    }

    public AdminAdventureDto? UpdateAdventure(Guid adventureId, AdminAdventureUpsertRequest request)
    {
        lock (_gate)
        {
            var index = _adventures.FindIndex(x => x.Id == adventureId);
            if (index < 0) return null;

            var current = _adventures[index];
            var objectives = NormalizeObjectives(request.Objectives);
            var updated = current with
            {
                Title = Normalize(request.Title, current.Title),
                Description = Normalize(request.Description, current.Description),
                Scope = Normalize(request.Scope, current.Scope),
                IsActive = request.IsActive,
                RewardPoints = objectives.Sum(x => x.RewardPoints),
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Objectives = objectives
            };
            _adventures[index] = updated;
            SaveState();
            return updated;
        }
    }

    public bool DeleteAdventure(Guid adventureId)
    {
        lock (_gate)
        {
            var removed = _adventures.RemoveAll(x => x.Id == adventureId) > 0;
            if (removed) SaveState();
            return removed;
        }
    }

    private bool TryLoadState(bool defaultDemoSignals)
    {
        try
        {
            if (!File.Exists(_statePath)) return false;

            var state = JsonSerializer.Deserialize<AdminOpsPersistedState>(
                File.ReadAllText(_statePath),
                _jsonOptions);
            if (state is null) return false;

            _demoSignalsEnabled = state.DemoSignalsEnabled ?? defaultDemoSignals;
            _testUsersEnabled = state.TestUsersEnabled ?? true;
            _adventures.Clear();
            _adventures.AddRange(state.Adventures?.Where(x => x.Objectives.Count > 0) ?? []);
            if (_adventures.Count == 0)
            {
                _adventures.AddRange(DefaultAdventures());
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void SaveState()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
            var state = new AdminOpsPersistedState(_demoSignalsEnabled, _testUsersEnabled, _adventures);
            File.WriteAllText(_statePath, JsonSerializer.Serialize(state, _jsonOptions));
        }
        catch
        {
            // Runtime controls must not break the API if the filesystem is read-only.
        }
    }

    private static List<AdminAdventureDto> DefaultAdventures()
    {
        var now = DateTimeOffset.UtcNow;
        return
        [
            new AdminAdventureDto(
                Guid.NewGuid(),
                "Settimana Local Hero",
                "Fai vivere tre locali diversi e posta una storia taggata.",
                "weekly",
                true,
                370,
                now,
                now,
                [
                    new AdminObjectiveDto(Guid.NewGuid(), "Visita 3 locali", "check_in", 3, 120, true),
                    new AdminObjectiveDto(Guid.NewGuid(), "Posta 2 stories in venue", "venue_story", 2, 100, true),
                    new AdminObjectiveDto(Guid.NewGuid(), "Lascia 2 recensioni verificate", "verified_rating", 2, 90, true),
                    new AdminObjectiveDto(Guid.NewGuid(), "Crea o joina un tavolo", "table_join", 1, 60, true)
                ]),
            new AdminAdventureDto(
                Guid.NewGuid(),
                "Accendi il gruppo",
                "Spingi inviti, flare e tavoli sociali nella tua zona.",
                "social",
                true,
                300,
                now,
                now,
                [
                    new AdminObjectiveDto(Guid.NewGuid(), "Rilancia 1 flare", "flare_relay", 1, 80, true),
                    new AdminObjectiveDto(Guid.NewGuid(), "Invita 3 amici", "invite_friend", 3, 100, true),
                    new AdminObjectiveDto(Guid.NewGuid(), "Partecipa a un tavolo", "table_join", 1, 120, true)
                ])
        ];
    }

    private static List<AdminObjectiveDto> NormalizeObjectives(IEnumerable<AdminObjectiveUpsertRequest>? objectives)
    {
        var normalized = (objectives ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.Title))
            .Select(x => new AdminObjectiveDto(
                x.Id == Guid.Empty ? Guid.NewGuid() : x.Id,
                Normalize(x.Title, "Obiettivo"),
                Normalize(x.MetricKey, "check_in"),
                Math.Clamp(x.Target, 1, 100),
                Math.Clamp(x.RewardPoints, 0, 10_000),
                x.IsActive))
            .Take(12)
            .ToList();

        if (normalized.Count == 0)
        {
            normalized.Add(new AdminObjectiveDto(Guid.NewGuid(), "Visita un locale", "check_in", 1, 50, true));
        }

        return normalized;
    }

    private static string Normalize(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private sealed record AdminOpsPersistedState(
        bool? DemoSignalsEnabled,
        bool? TestUsersEnabled,
        List<AdminAdventureDto>? Adventures);
}
