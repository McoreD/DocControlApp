import { useProject } from '../lib/projectContext';
import { CodesApi, DocumentsApi } from '../lib/api';
import { useState } from 'react';

export default function ImportView() {
  const { projectId } = useProject();
  const [codeCsv, setCodeCsv] = useState('');
  const [codeJson, setCodeJson] = useState('[]');
  const [codeExport, setCodeExport] = useState('');
  const [docLines, setDocLines] = useState('');
  const [docCsv, setDocCsv] = useState('Code,FreeText\nDFT-GOV-PLN-001,Governance Plan');
  const [docJson, setDocJson] = useState('[]');
  const [docExport, setDocExport] = useState('');
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loadingCodes, setLoadingCodes] = useState(false);
  const [loadingDocs, setLoadingDocs] = useState(false);
  const [loadingExports, setLoadingExports] = useState(false);

  const importCodes = async () => {
    if (!projectId) return;
    setLoadingCodes(true);
    setError(null);
    setMessage(null);
    try {
      await CodesApi.importCsv(projectId, codeCsv);
      setMessage('Codes imported (CSV).');
    } catch (err: any) {
      setError(err.message ?? 'Code import failed');
    } finally {
      setLoadingCodes(false);
    }
  };

  const importCodesJson = async () => {
    if (!projectId) return;
    setLoadingCodes(true);
    setError(null);
    setMessage(null);
    try {
      const parsed = JSON.parse(codeJson);
      await CodesApi.importJson(projectId, parsed);
      setMessage('Codes imported (JSON).');
    } catch (err: any) {
      setError(err.message ?? 'Code import failed');
    } finally {
      setLoadingCodes(false);
    }
  };

  const exportCodes = async () => {
    if (!projectId) return;
    setLoadingExports(true);
    setError(null);
    setMessage(null);
    try {
      const data = await CodesApi.exportJson(projectId);
      setCodeExport(JSON.stringify(data, null, 2));
      setMessage('Codes exported.');
    } catch (err: any) {
      setError(err.message ?? 'Export failed');
    } finally {
      setLoadingExports(false);
    }
  };

  const importDocs = async () => {
    if (!projectId) return;
    const entries = docLines
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

  const importDocsCsv = async () => {
    if (!projectId) return;
    setLoadingDocs(true);
    setError(null);
    setMessage(null);
    try {
      await DocumentsApi.importCsv(projectId, docCsv);
      setMessage('Documents imported (CSV).');
    } catch (err: any) {
      setError(err.message ?? 'Document import failed');
    } finally {
      setLoadingDocs(false);
    }
  };

  const importDocsJson = async () => {
    if (!projectId) return;
    setLoadingDocs(true);
    setError(null);
    setMessage(null);
    try {
      const parsed = JSON.parse(docJson);
      await DocumentsApi.importJson(projectId, parsed);
      setMessage('Documents imported (JSON).');
    } catch (err: any) {
      setError(err.message ?? 'Document import failed');
    } finally {
      setLoadingDocs(false);
    }
  };

  const exportDocs = async () => {
    if (!projectId) return;
    setLoadingExports(true);
    setError(null);
    setMessage(null);
    try {
      const data = await DocumentsApi.exportJson(projectId);
      setDocExport(JSON.stringify(data, null, 2));
      setMessage('Documents exported.');
    } catch (err: any) {
      setError(err.message ?? 'Export failed');
    } finally {
      setLoadingExports(false);
    }
  };

  return (
    <div className="page">
      <h1>Import</h1>
      <p className="muted">Import/export codes and documents via CSV, JSON, or line format.</p>
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
            value={codeCsv}
            onChange={(e) => setCodeCsv(e.target.value)}
            style={{ width: '100%', marginTop: 8 }}
          />
          <button onClick={importCodes} disabled={!projectId || loadingCodes}>
            {loadingCodes ? 'Working...' : 'Import Codes'}
          </button>
          <strong style={{ marginTop: 12, display: 'block' }}>Code JSON</strong>
          <textarea
            rows={6}
            placeholder='[{"level1":"DFT","level2":"GOV","level3":"REG","description":"Registers"}]'
            value={codeJson}
            onChange={(e) => setCodeJson(e.target.value)}
            style={{ width: '100%', marginTop: 8 }}
          />
          <button onClick={importCodesJson} disabled={!projectId || loadingCodes}>
            {loadingCodes ? 'Working...' : 'Import Codes (JSON)'}
          </button>
          <button onClick={exportCodes} disabled={!projectId || loadingExports} style={{ marginTop: 8 }}>
            {loadingExports ? 'Working...' : 'Export Codes (JSON)'}
          </button>
          {codeExport && (
            <textarea readOnly rows={6} value={codeExport} style={{ width: '100%', marginTop: 8 }} />
          )}
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
            value={docLines}
            onChange={(e) => setDocLines(e.target.value)}
            style={{ width: '100%', marginTop: 8 }}
          />
          <button onClick={importDocs} disabled={!projectId || loadingDocs}>
            {loadingDocs ? 'Working...' : 'Import Documents'}
          </button>
          <strong style={{ marginTop: 12, display: 'block' }}>Document CSV (Code,FreeText)</strong>
          <textarea
            rows={6}
            placeholder="Code,FreeText"
            value={docCsv}
            onChange={(e) => setDocCsv(e.target.value)}
            style={{ width: '100%', marginTop: 8 }}
          />
          <button onClick={importDocsCsv} disabled={!projectId || loadingDocs}>
            {loadingDocs ? 'Working...' : 'Import Documents (CSV)'}
          </button>
          <strong style={{ marginTop: 12, display: 'block' }}>Document JSON</strong>
          <textarea
            rows={6}
            placeholder='[{"code":"DFT-GOV-PLN-001","freeText":"Governance Plan"}]'
            value={docJson}
            onChange={(e) => setDocJson(e.target.value)}
            style={{ width: '100%', marginTop: 8 }}
          />
          <button onClick={importDocsJson} disabled={!projectId || loadingDocs}>
            {loadingDocs ? 'Working...' : 'Import Documents (JSON)'}
          </button>
          <button onClick={exportDocs} disabled={!projectId || loadingExports} style={{ marginTop: 8 }}>
            {loadingExports ? 'Working...' : 'Export Documents (JSON)'}
          </button>
          {docExport && (
            <textarea readOnly rows={6} value={docExport} style={{ width: '100%', marginTop: 8 }} />
          )}
        </div>
      </div>
      <p className="muted" style={{ marginTop: 12 }}>
        API endpoints ready: POST <code>/api/projects/{"{projectId}"}/codes/import</code>, <code>/codes/import/json</code>,
        <code>/documents/import</code>, <code>/documents/import/csv</code>, <code>/documents/import/json</code>,
        and exports at <code>/codes/export</code>, <code>/documents/export</code>.
      </p>
    </div>
  );
}
