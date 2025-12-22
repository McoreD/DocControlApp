import { useState } from 'react';

export default function Generate() {
  const [level1, setLevel1] = useState('');
  const [level2, setLevel2] = useState('');
  const [level3, setLevel3] = useState('');
  const [level4, setLevel4] = useState('');
  const [freeText, setFreeText] = useState('');
  const [output, setOutput] = useState<string | null>(null);

  const generate = () => {
    const code = [level1, level2, level3, level4].filter(Boolean).join('-');
    setOutput(`${code}-001 ${freeText}`.trim());
  };

  return (
    <div className="page">
      <h1>Generate</h1>
      <p className="muted">Fill in levels and free text; backend will allocate the next number.</p>
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
          <button onClick={generate}>Preview filename</button>
        </div>
        <div className="card">
          <strong>Preview</strong>
          <p className="muted">Not saved; for demo only.</p>
          <div style={{ marginTop: 10, fontWeight: 700 }}>{output ?? '...'}</div>
        </div>
      </div>
    </div>
  );
}
