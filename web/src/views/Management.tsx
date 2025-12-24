import { useState } from 'react';
import { CodesApi, DocumentsApi } from '../lib/api';
import { useProject } from '../lib/projectContext';

type FilePickOptions = { accept: string };

async function pickFileText(opts: FilePickOptions): Promise<string | null> {
  return new Promise((resolve) => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = opts.accept;
    input.style.display = 'none';
    input.onchange = () => {
      const file = input.files?.[0];
      if (!file) {
        resolve(null);
        return;
      }
      const reader = new FileReader();
      reader.onload = () => resolve(typeof reader.result === 'string' ? reader.result : null);
      reader.onerror = () => resolve(null);
      reader.readAsText(file);
    };
    document.body.appendChild(input);
    input.click();
    setTimeout(() => document.body.removeChild(input), 0);
  });
}

function downloadFile(contents: string, filename: string, mime = 'text/plain') {
  const blob = new Blob([contents], { type: mime });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}

export default function Management() {
  const { projectId } = useProject();
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const resetStatus = () => {
    setMessage(null);
    setError(null);
  };

  const importCodesCsv = async () => {
    if (!projectId) return;
    const text = await pickFileText({ accept: '.csv,text/csv' });
    if (!text) return;
    setLoading(true);
    resetStatus();
    try {
      await CodesApi.importCsv(projectId, text);
      setMessage('Codes imported from CSV.');
    } catch (err: any) {
      setError(err.message ?? 'Code import failed');
    } finally {
      setLoading(false);
    }
  };

  const importCodesJson = async () => {
    if (!projectId) return;
    const text = await pickFileText({ accept: '.json,application/json' });
    if (!text) return;
    setLoading(true);
    resetStatus();
    try {
      const parsed = JSON.parse(text);
      await CodesApi.importJson(projectId, parsed);
      setMessage('Codes imported from JSON.');
    } catch (err: any) {
      setError(err.message ?? 'Code import failed');
    } finally {
      setLoading(false);
    }
  };

  const exportCodesJson = async () => {
    if (!projectId) return;
    setLoading(true);
    resetStatus();
    try {
      const data = await CodesApi.exportJson(projectId);
      downloadFile(JSON.stringify(data, null, 2), 'codes.json', 'application/json');
      setMessage('Codes exported to JSON.');
    } catch (err: any) {
      setError(err.message ?? 'Export failed');
    } finally {
      setLoading(false);
    }
  };

  const exportCodesCsv = async () => {
    if (!projectId) return;
    setLoading(true);
    resetStatus();
    try {
      const data = await CodesApi.exportJson(projectId);
      const headers = ['Level1', 'Level2', 'Level3', 'Level4', 'Description', 'NextNumber'];
      const rows = data.map((c: any) =>
        [c.level1, c.level2, c.level3, c.level4 ?? '', c.description ?? '', c.nextNumber ?? ''].map((v) =>
          `"${String(v ?? '').replace(/"/g, '""')}"`
        ).join(',')
      );
      const csv = [headers.join(','), ...rows].join('\n');
      downloadFile(csv, 'codes.csv', 'text/csv');
      setMessage('Codes exported to CSV.');
    } catch (err: any) {
      setError(err.message ?? 'Export failed');
    } finally {
      setLoading(false);
    }
  };

  const importDocsCsv = async () => {
    if (!projectId) return;
    const text = await pickFileText({ accept: '.csv,text/csv' });
    if (!text) return;
    setLoading(true);
    resetStatus();
    try {
      await DocumentsApi.importCsv(projectId, text);
      setMessage('Documents imported from CSV.');
    } catch (err: any) {
      setError(err.message ?? 'Document import failed');
    } finally {
      setLoading(false);
    }
  };

  const importDocsJson = async () => {
    if (!projectId) return;
    const text = await pickFileText({ accept: '.json,application/json' });
    if (!text) return;
    setLoading(true);
    resetStatus();
    try {
      const parsed = JSON.parse(text);
      await DocumentsApi.importJson(projectId, parsed);
      setMessage('Documents imported from JSON.');
    } catch (err: any) {
      setError(err.message ?? 'Document import failed');
    } finally {
      setLoading(false);
    }
  };

  const exportDocsJson = async () => {
    if (!projectId) return;
    setLoading(true);
    resetStatus();
    try {
      const data = await DocumentsApi.exportJson(projectId);
      downloadFile(JSON.stringify(data, null, 2), 'documents.json', 'application/json');
      setMessage('Documents exported to JSON.');
    } catch (err: any) {
      setError(err.message ?? 'Export failed');
    } finally {
      setLoading(false);
    }
  };

  const exportDocsCsv = async () => {
    if (!projectId) return;
    setLoading(true);
    resetStatus();
    try {
      const data = await DocumentsApi.exportJson(projectId);
      const headers = ['Code', 'FreeText', 'FileName', 'CreatedAtUtc'];
      const rows = data.map((d: any) =>
        [d.code, d.freeText ?? '', d.fileName ?? '', d.createdAtUtc ?? ''].map((v) =>
          `"${String(v ?? '').replace(/"/g, '""')}"`
        ).join(',')
      );
      const csv = [headers.join(','), ...rows].join('\n');
      downloadFile(csv, 'documents.csv', 'text/csv');
      setMessage('Documents exported to CSV.');
    } catch (err: any) {
      setError(err.message ?? 'Export failed');
    } finally {
      setLoading(false);
    }
  };

  const purgeDocuments = async () => {
    if (!projectId) return;
    const confirmed = window.confirm('This will permanently delete all documents in this project. Continue?');
    if (!confirmed) return;
    setLoading(true);
    resetStatus();
    try {
      const result = await DocumentsApi.purge(projectId);
      setMessage(`Deleted ${result.deleted} document(s).`);
    } catch (err: any) {
      setError(err.message ?? 'Failed to purge documents');
    } finally {
      setLoading(false);
    }
  };

  const purgeCodes = async () => {
    if (!projectId) return;
    const confirmed = window.confirm('Permanently delete all codes (and their documents). Continue?');
    if (!confirmed) return;
    setLoading(true);
    resetStatus();
    try {
      const result = await CodesApi.purge(projectId);
      setMessage(`Deleted ${result.deletedCodes} code(s) and ${result.deletedDocuments} document(s).`);
    } catch (err: any) {
      setError(err.message ?? 'Failed to purge codes');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="page">
      <h1>Management</h1>
      <p className="muted">Import, export, and purge data for the current project.</p>
      {!projectId && <div className="pill">Select a project first.</div>}
      {message && <div className="pill" style={{ background: '#ecfdf3', color: '#166534' }}>{message}</div>}
      {error && <div className="pill" style={{ background: '#fee2e2', color: '#991b1b' }}>{error}</div>}

      {projectId && (
        <>
          <div className="card" style={{ marginTop: 12 }}>
            <strong>Import</strong>
            <p className="muted">Codes</p>
            <div className="row" style={{ gap: 8 }}>
              <button onClick={importCodesCsv} disabled={loading}>Import Codes CSV</button>
              <button onClick={importCodesJson} disabled={loading}>Import Codes JSON</button>
            </div>
            <p className="muted" style={{ marginTop: 12 }}>Documents</p>
            <div className="row" style={{ gap: 8 }}>
              <button onClick={importDocsCsv} disabled={loading}>Import Documents CSV</button>
              <button onClick={importDocsJson} disabled={loading}>Import Documents JSON</button>
            </div>
          </div>

          <div className="card" style={{ marginTop: 12 }}>
            <strong>Export</strong>
            <p className="muted">Codes</p>
            <div className="row" style={{ gap: 8 }}>
              <button onClick={exportCodesCsv} disabled={loading}>Export Codes CSV</button>
              <button onClick={exportCodesJson} disabled={loading}>Export Codes JSON</button>
            </div>
            <p className="muted" style={{ marginTop: 12 }}>Documents</p>
            <div className="row" style={{ gap: 8 }}>
              <button onClick={exportDocsCsv} disabled={loading}>Export Documents CSV</button>
              <button onClick={exportDocsJson} disabled={loading}>Export Documents JSON</button>
            </div>
          </div>

          <div className="card" style={{ marginTop: 12 }}>
            <strong>Purge</strong>
            <p className="muted" style={{ margin: '8px 0' }}>
              Purge removes data permanently for this project.
            </p>
            <div className="row" style={{ gap: 8, flexWrap: 'wrap' }}>
              <button onClick={purgeDocuments} disabled={loading} style={{ background: '#b91c1c' }}>
                {loading ? 'Purging...' : 'Purge documents'}
              </button>
              <button onClick={purgeCodes} disabled={loading} style={{ background: '#b91c1c' }}>
                {loading ? 'Purging...' : 'Purge codes'}
              </button>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
