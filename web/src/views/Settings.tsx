export default function Settings() {
  return (
    <div className="page">
      <h1>Settings</h1>
      <p className="muted">
        Project settings: separator, padding, enable level4, AI provider/model + keys. Endpoint: GET/POST
        <code>/api/projects/{"{projectId}"}/settings</code>.
      </p>
      <div className="card">
        <p className="muted">UI wiring pending. Set AI keys via POST payload fields openAiKey / geminiKey.</p>
      </div>
    </div>
  );
}
