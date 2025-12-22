export default function Documents() {
  return (
    <div className="page">
      <h1>Documents</h1>
      <p className="muted">List and filter documents per project. Filters: level1, level2, level3, q (file/free text).</p>
      <div className="card">
        <p className="muted">
          UI hook-up to API pending. Use GET <code>/api/projects/{"{projectId}"}/documents?level1=&amp;level2=&amp;level3=&amp;q=</code> to fetch.
        </p>
      </div>
    </div>
  );
}
