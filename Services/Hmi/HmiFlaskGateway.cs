namespace etch_ui.Services.Hmi;

/// <summary>Flask 헬스·이벤트·AI 조회 (HTTP만).</summary>
public sealed class HmiFlaskGateway
{
    private readonly EtchFlaskClient _flask;

    public HmiFlaskGateway(EtchFlaskClient flask) => _flask = flask;

    public string BaseUrl
    {
        get => _flask.BaseUrl;
        set => _flask.BaseUrl = value;
    }

    public Task<bool> ProbeHealthAsync(CancellationToken cancellationToken = default) =>
        _flask.TryHealthCheckAsync(cancellationToken);

    public Task<bool> PublishEventAsync(
        IReadOnlyList<FlaskEventItem> items,
        string dataSource,
        CancellationToken cancellationToken = default) =>
        _flask.TryPostEtchEventsAsync(items, dataSource, cancellationToken);

    public Task<EtchAiDiagnosis?> PollAiLatestAsync(CancellationToken cancellationToken = default) =>
        _flask.TryGetAiLatestAsync(cancellationToken);
}
