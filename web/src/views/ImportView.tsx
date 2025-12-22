export default function ImportView() {
  return (
    <div className="page">
      <h1>Import</h1>
      <p className="muted">Import codes (CSV) or documents (code + file name lines).</p>
      <div className="grid">
        <div className="card">
          <strong>Code CSV format</strong>
          <pre style={{ whiteSpace: 'pre-wrap' }}>
{`Level,Code,Code Description
1,DFT,Delpach Family Trust
2,GOV,Governance
3,REG,Registers`}
          </pre>
        </div>
        <div className="card">
          <strong>Document import (code + filename)</strong>
          <pre style={{ whiteSpace: 'pre-wrap' }}>
{`DFT-GOV-REG-001 Delpach DocControl.pdf
DFT-GOV-REG-002 Another file.docx`}
          </pre>
        </div>
      </div>
      <p className="muted" style={{ marginTop: 12 }}>
        API endpoints ready: POST <code>/api/projects/{"{projectId}"}/codes/import</code> and
        <code>/api/projects/{"{projectId}"}/documents/import</code>.
      </p>
    </div>
  );
}
