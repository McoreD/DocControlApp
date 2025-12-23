import { useProject } from '../lib/projectContext';
import { CodesApi, DocumentsApi } from '../lib/api';
import { useState } from 'react';

export default function ImportView() {
  const { projectId } = useProject();
  const [csv, setCsv] = useState('');
  const [lines, setLines] = useState('');
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loadingCodes, setLoadingCodes] = useState(false);
  const [loadingDocs, setLoadingDocs] = useState(false);

  const importCodes = async () => {
    if (!projectId) return;
    setLoadingCodes(true);
    setError(null);
    setMessage(null);
    try {
      await CodesApi.importCsv(projectId, csv);
      setMessage('Codes imported.');
    } catch (err: any) {
      setError(err.message ?? 'Code import failed');
    } finally {
      setLoadingCodes(false);
    }
  };

  const importDocs = async () => {
    if (!projectId) return;
    const entries = lines
      .split('\n')
      .map((l: string) => l.trim())
      .filter(Boolean)
      .map((line: string) => {
        // Expect "CODE rest of filename", keep the full line as the file name and the tail as free text
        const match = line.match(/^(\S+)\s+(.+)$/);
        if (!match) return { code: line, fileName: line };
        const [, code, tail] = match;
        return { code, fileName: line, freeText: tail };
    });
    if (entries.length === 0) return;
    setLoadingDocs(true);
    setError(null);
    setMessage(null);
    try {
      await DocumentsApi.importSimple(projectId, entries);
      setMessage('Documents imported.');
    } catch (err: any) {
      setError(err.message ?? 'Document import failed');
    } finally {
      setLoadingDocs(false);
    }
  };

  return (
    <div className="page">
      <h1>Import</h1>
      <p className="muted">Import codes (CSV) or documents (code + file name lines).</p>
      {!projectId && <div className="pill">Select a project first.</div>}
      {message && <div className="pill" style={{ background: '#ecfdf3', color: '#166534' }}>{message}</div>}
      {error && <div className="pill" style={{ background: '#fee2e2', color: '#991b1b' }}>{error}</div>}
      <div className="grid">
        <div className="card">
          <strong>Code CSV format</strong>
          <pre style={{ whiteSpace: 'pre-wrap' }}>
{`Level,Code,Code Description
1,DFT,Delpach Family Trust
2,GOV,Governance
3,REG,Registers`}
          </pre>
          <textarea
            rows={6}
            placeholder="Paste CSV content here"
            value={csv}
            onChange={(e) => setCsv(e.target.value)}
            style={{ width: '100%', marginTop: 8 }}
          />
          <button onClick={importCodes} disabled={!projectId || loadingCodes}>
            {loadingCodes ? 'Working...' : 'Import Codes'}
          </button>
        </div>
        <div className="card">
          <strong>Document import (code + filename)</strong>
          <pre style={{ whiteSpace: 'pre-wrap' }}>
{`DFT-GOV-REG-001 Delpach DocControl.pdf
DFT-GOV-REG-002 Another file.docx`}
          </pre>
          <textarea
            rows={6}
            placeholder="Paste lines here"
            value={lines}
            onChange={(e) => setLines(e.target.value)}
            style={{ width: '100%', marginTop: 8 }}
          />
          <button onClick={importDocs} disabled={!projectId || loadingDocs}>
            {loadingDocs ? 'Working...' : 'Import Documents'}
          </button>
        </div>
      </div>
      <p className="muted" style={{ marginTop: 12 }}>
        API endpoints ready: POST <code>/api/projects/{"{projectId}"}/codes/import</code> and
        <code>/api/projects/{"{projectId}"}/documents/import</code>.
      </p>
    </div>
  );
}
