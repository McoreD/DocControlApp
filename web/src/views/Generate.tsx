import { useState } from 'react';
import { DocumentsApi } from '../lib/api';
import { useProject } from '../lib/projectContext';

export default function Generate() {
  const [level1, setLevel1] = useState('');
  const [level2, setLevel2] = useState('');
  const [level3, setLevel3] = useState('');
  const [level4, setLevel4] = useState('');
  const [freeText, setFreeText] = useState('');
  const [output, setOutput] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const { projectId } = useProject();

  const generate = async () => {
    if (!projectId) return;
    setLoading(true);
    setError(null);
    try {
      const res = await DocumentsApi.create(projectId, {
        level1,
        level2,
        level3,
        level4: level4 || undefined,
        freeText,
        extension: undefined,
      });
      setOutput(res.fileName ?? `(created) #${res.number}`);
    } catch (err: any) {
      setError(err.message ?? 'Failed to create document');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="page">
      <h1>Generate</h1>
      <p className="muted">Fill in levels and free text; backend will allocate the next number.</p>
      {!projectId && <div className="pill">Select a project first.</div>}
      <div className="grid">
        <div className="stack">
          <label>Level1</label>
          <input value={level1} onChange={(e) => setLevel1(e.target.value)} placeholder="DFT" />
          <label>Level2</label>
          <input value={level2} onChange={(e) => setLevel2(e.target.value)} placeholder="GOV" />
          <label>Level3</label>
          <input value={level3} onChange={(e) => setLevel3(e.target.value)} placeholder="REG" />
          <label>Level4 (optional)</label>
          <input value={level4} onChange={(e) => setLevel4(e.target.value)} placeholder="SUB" />
          <label>Free text</label>
          <input value={freeText} onChange={(e) => setFreeText(e.target.value)} placeholder="Delpach DocControl" />
          <button onClick={generate} disabled={!projectId || loading}>
            {loading ? 'Saving...' : 'Create document'}
          </button>
          {error && <div className="pill" style={{ background: '#fee2e2', color: '#991b1b' }}>{error}</div>}
        </div>
        <div className="card">
          <strong>Preview</strong>
          <p className="muted">Filename returned by backend.</p>
          <div style={{ marginTop: 10, fontWeight: 700 }}>{output ?? '...'}</div>
        </div>
      </div>
    </div>
  );
}
