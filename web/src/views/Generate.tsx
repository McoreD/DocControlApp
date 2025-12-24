import { useState } from 'react';
import { AiApi, DocumentsApi } from '../lib/api';
import { useProject } from '../lib/projectContext';

export default function Generate() {
  const [query, setQuery] = useState('');
  const [recommendResult, setRecommendResult] = useState<any | null>(null);
  const [recommendError, setRecommendError] = useState<string | null>(null);
  const [recommendLoading, setRecommendLoading] = useState(false);
  const [level1, setLevel1] = useState('');
  const [level2, setLevel2] = useState('');
  const [level3, setLevel3] = useState('');
  const [freeText, setFreeText] = useState('');
  const [output, setOutput] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [previewLoading, setPreviewLoading] = useState(false);
  const { projectId } = useProject();

  const runRecommend = async () => {
    if (!projectId || !query.trim()) return;
    setRecommendLoading(true);
    setRecommendError(null);
    try {
      const res = await AiApi.recommend(projectId, query.trim());
      setRecommendResult(res);
      setLevel1(res.level1 ?? '');
      setLevel2(res.level2 ?? '');
      setLevel3(res.level3 ?? '');
      setFreeText(res.freeText ?? '');
    } catch (err: any) {
      setRecommendError(err.message ?? 'AI recommend failed');
    } finally {
      setRecommendLoading(false);
    }
  };

  const generate = async () => {
    if (!projectId) return;
    setLoading(true);
    setError(null);
    try {
      const res = await DocumentsApi.create(projectId, {
        level1,
        level2,
        level3,
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

  const preview = async () => {
    if (!projectId) return;
    setPreviewLoading(true);
    setError(null);
    try {
      const res = await DocumentsApi.preview(projectId, {
        level1,
        level2,
        level3,
        freeText,
        extension: undefined,
      });
      setOutput(res.fileName ?? '');
    } catch (err: any) {
      setError(err.message ?? 'Preview failed');
    } finally {
      setPreviewLoading(false);
    }
  };

  return (
    <div className="page">
      <h1>Generate</h1>
      <p className="muted">Use AI to suggest a code, then create the document with the next number.</p>
      {!projectId && <div className="pill">Select a project first.</div>}
      <div className="card">
        <strong>AI Recommend</strong>
        <p className="muted">Ask in natural language; we will suggest code levels and free text.</p>
        <textarea
          rows={3}
          placeholder="e.g. 'Create governance register for Delpach Family Trust'"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
        />
        <button onClick={runRecommend} disabled={!query.trim() || !projectId || recommendLoading}>
          {recommendLoading ? 'Working...' : 'Recommend'}
        </button>
        {recommendError && <div className="pill" style={{ background: '#fee2e2', color: '#991b1b' }}>{recommendError}</div>}
        {recommendResult && (
          <div className="card" style={{ marginTop: 10 }}>
            <div><strong>Level1:</strong> {recommendResult.level1}</div>
            <div><strong>Level2:</strong> {recommendResult.level2}</div>
            <div><strong>Level3:</strong> {recommendResult.level3}</div>
            {recommendResult.level4 && <div><strong>Level4:</strong> {recommendResult.level4}</div>}
            <div><strong>Free text:</strong> {recommendResult.freeText}</div>
            {recommendResult.reason && <div className="muted">Reason: {recommendResult.reason}</div>}
          </div>
        )}
      </div>
      <div className="grid" style={{ marginTop: 12 }}>
        <div className="stack">
          <strong>Generate next code</strong>
          <p className="muted">Fill in levels and free text; backend will allocate the next number.</p>
          <label>Level1</label>
          <input value={level1} onChange={(e) => setLevel1(e.target.value)} placeholder="DFT" />
          <label>Level2</label>
          <input value={level2} onChange={(e) => setLevel2(e.target.value)} placeholder="GOV" />
          <label>Level3</label>
          <input value={level3} onChange={(e) => setLevel3(e.target.value)} placeholder="REG" />
          <label>Free text</label>
          <input value={freeText} onChange={(e) => setFreeText(e.target.value)} placeholder="Delpach DocControl" />
          <div className="row" style={{ gap: 8, flexWrap: 'wrap' }}>
            <button onClick={preview} disabled={!projectId || previewLoading}>
              {previewLoading ? 'Previewing...' : 'Preview'}
            </button>
            <button onClick={generate} disabled={!projectId || loading}>
              {loading ? 'Saving...' : 'Create document'}
            </button>
          </div>
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
