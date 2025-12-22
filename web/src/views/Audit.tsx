export default function Audit() {
  return (
    <div className="page">
      <h1>Audit</h1>
      <p className="muted">
        Paged audit trail per project. Endpoint: <code>/api/projects/{"{projectId}"}/audit</code>
      </p>
      <div className="card">
        <p className="muted">UI wiring to API pending. Will show action, user, timestamp, and payload.</p>
      </div>
    </div>
  );
}
