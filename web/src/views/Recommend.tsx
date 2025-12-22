import { useState } from 'react';

export default function Recommend() {
  const [query, setQuery] = useState('');
  const [result, setResult] = useState<string | null>(null);

  return (
    <div className="page">
      <h1>Recommend</h1>
      <p className="muted">Ask in natural language; the API will interpret and suggest codes.</p>
      <div className="stack">
        <textarea
          rows={4}
          placeholder="e.g. 'Create governance register for Delpach Family Trust'"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
        />
        <button onClick={() => setResult(`(stub) Would call /ai/recommend with: ${query}`)} disabled={!query.trim()}>
          Recommend
        </button>
        {result && <div className="card">{result}</div>}
      </div>
    </div>
  );
}
