import { useState } from 'react';
import { AiApi } from '../lib/api';
import { useProject } from '../lib/projectContext';

export default function Recommend() {
  const [query, setQuery] = useState('');
  const [result, setResult] = useState<any | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const { projectId } = useProject();

  const run = async () => {
    if (!projectId || !query.trim()) return;
    setLoading(true);
    setError(null);
    try {
      const res = await AiApi.recommend(projectId, query.trim());
      setResult(res);
    } catch (err: any) {
      setError(err.message ?? 'AI recommend failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="page">
      <h1>Recommend</h1>
      <p className="muted">Ask in natural language; the API will interpret and suggest codes.</p>
      {!projectId && <div className="pill">Select a project first.</div>}
      <div className="stack">
        <textarea
          rows={4}
          placeholder="e.g. 'Create governance register for Delpach Family Trust'"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
        />
        <button onClick={run} disabled={!query.trim() || !projectId || loading}>
          {loading ? 'Working...' : 'Recommend'}
        </button>
        {error && <div className="pill" style={{ background: '#fee2e2', color: '#991b1b' }}>{error}</div>}
        {result && (
          <div className="card">
            <div><strong>Level1:</strong> {result.level1}</div>
            <div><strong>Level2:</strong> {result.level2}</div>
            <div><strong>Level3:</strong> {result.level3}</div>
            {result.level4 && <div><strong>Level4:</strong> {result.level4}</div>}
            <div><strong>Free text:</strong> {result.freeText}</div>
            {result.reason && <div className="muted">Reason: {result.reason}</div>}
          </div>
        )}
      </div>
    </div>
  );
}
