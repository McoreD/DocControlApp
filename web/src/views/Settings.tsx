import { useEffect, useState } from 'react';
import { SettingsApi } from '../lib/api';
import { useProject } from '../lib/projectContext';

type DocumentConfig = {
  separator: string;
  paddingLength: number;
};

type AiSettings = {
  provider: string;
  openAiModel: string;
  geminiModel: string;
};

export default function Settings() {
  const { projectId } = useProject();
  const [docConfig, setDocConfig] = useState<DocumentConfig>({ separator: '-', paddingLength: 3 });
  const [ai, setAi] = useState<AiSettings>({ provider: 'OpenAi', openAiModel: 'gpt-4.1', geminiModel: 'gemini-3-flash-preview' });
  const [openAiKey, setOpenAiKey] = useState('');
  const [geminiKey, setGeminiKey] = useState('');
  const [hasOpenAiKey, setHasOpenAiKey] = useState(false);
  const [hasGeminiKey, setHasGeminiKey] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const load = async () => {
      if (!projectId) return;
      setError(null);
      try {
        const data = await SettingsApi.get(projectId);
        setDocConfig(data.documentConfig);
        setAi(data.aiSettings);
        setHasOpenAiKey(data.hasOpenAiKey ?? false);
        setHasGeminiKey(data.hasGeminiKey ?? false);
      } catch (err: any) {
        setError(err.message ?? 'Failed to load settings');
      }
    };
    load();
  }, [projectId]);

  const save = async () => {
    if (!projectId) return;
    setError(null);
    setMessage(null);
    try {
      await SettingsApi.save(projectId, {
        documentConfig: docConfig,
        aiSettings: ai,
        openAiKey,
        geminiKey,
      });
      setMessage('Saved');
      setHasOpenAiKey(!!openAiKey || hasOpenAiKey);
      setHasGeminiKey(!!geminiKey || hasGeminiKey);
      setOpenAiKey('');
      setGeminiKey('');
    } catch (err: any) {
      setError(err.message ?? 'Failed to save');
    }
  };

  return (
    <div className="page">
      <h1>Settings</h1>
      <p className="muted">
        Project settings: separator, padding, enable level4, AI provider/model + keys. Endpoint: GET/POST
        <code>/api/projects/{"{projectId}"}/settings</code>.
      </p>
      {!projectId && <div className="pill">Select a project first.</div>}
      {message && <div className="pill" style={{ background: '#ecfdf3', color: '#166534' }}>{message}</div>}
      {error && <div className="pill" style={{ background: '#fee2e2', color: '#991b1b' }}>{error}</div>}
      {projectId && (
        <div className="grid">
          <div className="card">
            <h3>Document Config</h3>
            <div className="stack">
              <label>Separator</label>
              <input value={docConfig.separator} onChange={(e) => setDocConfig({ ...docConfig, separator: e.target.value })} />
              <label>Padding Length</label>
              <input
                type="number"
                value={docConfig.paddingLength}
                onChange={(e) => setDocConfig({ ...docConfig, paddingLength: Number(e.target.value) })}
              />
            </div>
          </div>
          <div className="card">
            <h3>AI</h3>
            <div className="stack">
              <label>Provider</label>
              <select value={ai.provider} onChange={(e) => setAi({ ...ai, provider: e.target.value })}>
                <option value="OpenAi">OpenAI</option>
                <option value="Gemini">Gemini</option>
              </select>
              <label>OpenAI Model</label>
              <input value={ai.openAiModel} onChange={(e) => setAi({ ...ai, openAiModel: e.target.value })} />
              <label>Gemini Model</label>
              <input value={ai.geminiModel} onChange={(e) => setAi({ ...ai, geminiModel: e.target.value })} />
              <label>OpenAI Key (optional)</label>
              <input value={openAiKey} onChange={(e) => setOpenAiKey(e.target.value)} placeholder={hasOpenAiKey ? 'Key stored' : undefined} />
              {hasOpenAiKey && <span className="muted">Key is stored.</span>}
              <label>Gemini Key (optional)</label>
              <input value={geminiKey} onChange={(e) => setGeminiKey(e.target.value)} placeholder={hasGeminiKey ? 'Key stored' : undefined} />
              {hasGeminiKey && <span className="muted">Key is stored.</span>}
            </div>
          </div>
        </div>
      )}
      {projectId && (
        <div style={{ marginTop: 12 }}>
          <button onClick={save}>Save</button>
        </div>
      )}
    </div>
  );
}
