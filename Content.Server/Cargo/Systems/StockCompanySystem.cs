using Content.Shared.GameTicking;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Cargo.Systems;

public sealed class StockCompanySystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly List<StockCompanyData> _companies = new();

    private TimeSpan _lastUpdate;

    private const float UpdateInterval = 15f;

    public const int MaxHistoryLength = 120;

    private const float MinMultiplier = 0.2f;
    private const float MaxMultiplier = 3.0f;

    private const float MeanReversionStrength = 0.005f;

    private const float MomentumCarry = 0.6f;

    private const float SentimentDriftStrength = 0.002f;

    private const float SentimentShiftChance = 0.025f;

    private const float SentimentLerpSpeed = 0.1f;

    private const float EventChancePerTick = 0.018f;

    private float _marketSentiment;
    private float _sentimentTarget;

    private bool _initialized;

    public override void Initialize()
    {
        base.Initialize();
        _lastUpdate = _timing.CurTime;
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _companies.Clear();
        _marketSentiment = 0f;
        _sentimentTarget = 0f;
        _initialized = false;
        _lastUpdate = _timing.CurTime;
    }

    private void InitializeCompanies()
    {
        if (_initialized)
            return;
        _initialized = true;

        _companies.Clear();
        _companies.Add(new StockCompanyData("stock-company-shi",  500f, 0.006f, 6000));
        _companies.Add(new StockCompanyData("stock-company-tfsc", 400f, 0.008f, 4000));
        _companies.Add(new StockCompanyData("stock-company-dsm",  350f, 0.013f, 2500));
        _companies.Add(new StockCompanyData("stock-company-ncwl", 250f, 0.020f, 1500));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_initialized)
            InitializeCompanies();

        var curTime = _timing.CurTime;
        if ((curTime - _lastUpdate).TotalSeconds < UpdateInterval)
            return;

        _lastUpdate = curTime;

        UpdateSentiment();

        foreach (var company in _companies)
        {
            var eventEffect = UpdateCompanyEvent(company);

            var noise = company.Volatility * NextGaussian();
            var sentiment = _marketSentiment * SentimentDriftStrength;
            var reversion = (1.0f - company.PriceMultiplier) * MeanReversionStrength;

            var organic = noise + company.Momentum + sentiment + reversion;
            company.Momentum = MomentumCarry * organic;

            var change = organic + eventEffect;
            company.PriceMultiplier = Math.Clamp(company.PriceMultiplier * (1f + change), MinMultiplier, MaxMultiplier);
            company.CurrentPrice = company.BasePrice * company.PriceMultiplier;
            company.PriceChange = company.PriceMultiplier - 1.0f;

            company.PriceHistory.Add(company.CurrentPrice);
            if (company.PriceHistory.Count > MaxHistoryLength)
                company.PriceHistory.RemoveAt(0);
        }

        var ev = new StockCompaniesUpdatedEvent();
        RaiseLocalEvent(ref ev);
    }

    private void UpdateSentiment()
    {
        if (_random.Prob(SentimentShiftChance))
            _sentimentTarget = _random.NextFloat(-1f, 1f);

        _marketSentiment += (_sentimentTarget - _marketSentiment) * SentimentLerpSpeed;
    }

    private float UpdateCompanyEvent(StockCompanyData company)
    {
        if (company.EventTicksRemaining > 0)
        {
            company.EventTicksRemaining--;
            return company.EventShiftPerTick;
        }

        if (!_random.Prob(EventChancePerTick))
            return 0f;

        var roll = _random.NextFloat();
        float magnitude;
        bool negative;
        if (roll < 0.55f)
        {
            magnitude = _random.NextFloat(0.04f, 0.08f);
            negative = _random.Prob(0.5f);
        }
        else if (roll < 0.82f)
        {
            magnitude = _random.NextFloat(0.10f, 0.18f);
            negative = _random.Prob(0.5f);
        }
        else if (roll < 0.96f)
        {
            magnitude = _random.NextFloat(0.20f, 0.35f);
            negative = _random.Prob(0.5f);
        }
        else
        {
            magnitude = _random.NextFloat(0.40f, 0.60f);
            negative = _random.Prob(0.7f);
        }

        var duration = _random.Next(4, 11);
        company.EventTicksRemaining = duration;
        company.EventShiftPerTick = (negative ? -magnitude : magnitude) / duration;
        return 0f;
    }

    private float NextGaussian()
    {
        var u1 = MathF.Max(_random.NextFloat(), 1e-6f);
        var u2 = _random.NextFloat();
        return MathF.Sqrt(-2f * MathF.Log(u1)) * MathF.Cos(2f * MathF.PI * u2);
    }

    public List<StockCompanyData> GetCompanies()
    {
        if (!_initialized)
            InitializeCompanies();
        return _companies;
    }

    public StockCompanyData? GetCompany(string id)
    {
        if (!_initialized)
            InitializeCompanies();
        return _companies.Find(c => c.Id == id);
    }
}

public sealed class StockCompanyData
{
    public string Id;
    public float BasePrice;
    public float CurrentPrice;
    public float PriceMultiplier = 1.0f;
    public float PriceChange;

    public readonly float Volatility;

    public readonly int Liquidity;

    public float Momentum;

    public float EventShiftPerTick;
    public int EventTicksRemaining;

    public readonly List<float> PriceHistory = new();

    public StockCompanyData(string id, float basePrice, float volatility, int liquidity)
    {
        Id = id;
        BasePrice = basePrice;
        CurrentPrice = basePrice;
        Volatility = volatility;
        Liquidity = liquidity;
        PriceHistory.Add(basePrice);
    }
}

[ByRefEvent]
public readonly record struct StockCompaniesUpdatedEvent;
