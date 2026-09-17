# 9Router Portable for Windows 🚀

A standalone, fully portable Windows x64 desktop launcher and wrapper for **[9Router](https://github.com/decolua/9router)**. Run 9Router instantly on Windows without manual Node.js installation or complex configuration!

---

## 🌟 Features

- **Zero-Install Node.js Runtime**: Bundles a standalone Node.js binary.
- **System Tray Integration**: Minimize to tray, start/stop server, and quick-launch dashboard from the tray icon.
- **Auto-Configuration**: Automatic generation of JWT secrets and initial credentials.
- **Network Access**: Exposes local and LAN access URLs easily.
- **Process Management**: Robust Windows Job Object tracking to ensure child processes are fully terminated when exiting.

---

## 📥 Quick Start

1. Download `9RouterPortable-win-x64.zip` from the [Releases](../../releases) page.
2. Extract the ZIP archive anywhere on your Windows machine (fully portable).
3. Run `9RouterPortable.exe`.
4. Click **START SERVER** and access the local or LAN dashboard link!

---

## 🛠️ Building From Source

### Prerequisites
- **.NET 8 SDK** (for compiling the Windows Forms launcher)
- **Node.js & npm** (for building upstream 9Router)
- **PowerShell 5.1+ or PowerShell 7+**

### Cloning & Nested Git Repositories
Because `upstream-9router` is a separate git repository, cloning normally might leave it empty or detached. Clone recursively:
```bash
git clone --recursive https://github.com/kyradais/9router-win-portable.git
```
If already cloned, initialize submodules:
```bash
git submodule update --init --recursive
```
*Note: If `upstream-9router` has its own `.git` directory and won't commit to your main repo, remove its `.git` folder so Git tracks it as a standard directory:*
```bash
rm -rf upstream-9router/.git
```

### Build Steps
1. Open PowerShell in the repository root.
2. Run the build script (you can use `powershell` if `pwsh` is not available):
   ```powershell
   powershell -ExecutionPolicy Bypass -File scripts/build-portable.ps1
   ```
   or with PowerShell Core (`pwsh`):
   ```powershell
   pwsh scripts/build-portable.ps1
   ```
3. Output package: `dist/9RouterPortable-win-x64.zip`.

---

## ⚙️ SQL.js Adapter Manual Fix & Guide
If `upstream-9router/src/lib/db/adapters/sqljsAdapter.js` is untracked or needs manual adjustment due to nested git submodules, ensure it has proper WASM resolution and debounced persistence:

```javascript
import fs from "node:fs";
import path from "node:path";
import initSqlJs from "sql.js";
import { PRAGMA_SQL } from "../schema.js";

let SQL = null;

async function loadSql() {
  if (SQL) return SQL;
  const possiblePaths = [
    path.join(process.cwd(), "sql-wasm.wasm"),
    path.join(process.cwd(), "node_modules", "sql.js", "dist", "sql-wasm.wasm")
  ];
  let wasmPath = possiblePaths.find(p => fs.existsSync(p));
  SQL = wasmPath ? await initSqlJs({ locateFile: () => wasmPath }) : await initSqlJs();
  return SQL;
}

export async function createSqlJsAdapter(filePath) {
  const SQLLib = await loadSql();
  const buf = fs.existsSync(filePath) ? fs.readFileSync(filePath) : null;
  const db = new SQLLib.Database(buf);
  db.exec(PRAGMA_SQL);

  let dirty = false;
  let saveTimer = null;

  function persist() {
    fs.writeFileSync(filePath, Buffer.from(db.export()));
    dirty = false;
  }

  function scheduleSave() {
    dirty = true;
    if (saveTimer) clearTimeout(saveTimer);
    saveTimer = setTimeout(() => { saveTimer = null; if (dirty) persist(); }, 100);
  }

  return {
    driver: "sql.js",
    run(sql, params = []) {
      const stmt = db.prepare(sql);
      try {
        stmt.bind(params.length ? params : undefined);
        stmt.step();
        const changes = db.getRowsModified();
        const lastInsertRowid = db.exec("SELECT last_insert_rowid() as id")[0]?.values?.[0]?.[0] ?? null;
        scheduleSave();
        return { changes, lastInsertRowid };
      } finally { stmt.free(); }
    },
    get(sql, params = []) {
      const stmt = db.prepare(sql);
      try {
        stmt.bind(params.length ? params : undefined);
        return stmt.step() ? stmt.getAsObject() : undefined;
      } finally { stmt.free(); }
    },
    all(sql, params = []) {
      const stmt = db.prepare(sql);
      try {
        stmt.bind(params.length ? params : undefined);
        const rows = [];
        while (stmt.step()) rows.push(stmt.getAsObject());
        return rows;
      } finally { stmt.free(); }
    },
    exec(sql) { db.exec(sql); scheduleSave(); },
    transaction(fn) {
      const sp = `sp_${Math.random().toString(36).slice(2)}`;
      db.exec(`SAVEPOINT ${sp}`);
      try {
        const res = fn();
        db.exec(`RELEASE ${sp}`);
        scheduleSave();
        return res;
      } catch (e) {
        try { db.exec(`ROLLBACK TO ${sp}`); db.exec(`RELEASE ${sp}`); } catch {}
        throw e;
      }
    },
    close() { if (saveTimer) clearTimeout(saveTimer); if (dirty) persist(); db.close(); },
    raw: db
  };
}
```

---

## 📜 Attribution & Legal

- **9Router Core**: Developed by [decolua](https://github.com/decolua/9router) under its respective open-source license.
- **9Router Portable**: Windows launcher wrapper built as an independent open-source utility.

---

## 📚 Documentation
- [Architecture & Design](PORTABLE_ARCHITECTURE.md)
- [Build Guide](BUILD.md)
- [Troubleshooting](TROUBLESHOOTING.md)

