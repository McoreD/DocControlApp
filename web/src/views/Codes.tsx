export default function Codes() {
  return (
    <div className="page">
      <h1>Codes</h1>
      <p className="muted">Import or manage code catalog per project. Use CSV format: Level,Code,Code Description.</p>
      <div className="card">
        <strong>Sample rows</strong>
        <pre style={{ whiteSpace: 'pre-wrap' }}>
{`Level,Code,Code Description
1,DFT,Delpach Family Trust
2,GOV,Governance
3,REG,Registers`}
        </pre>
      </div>
      <p className="muted" style={{ marginTop: 12 }}>
        UI wiring to the API is pending; endpoints are available at <code>/api/projects/{"{projectId}"}/codes</code> and
        <code>/api/projects/{"{projectId}"}/codes/import</code>.
      </p>
    </div>
  );
}
