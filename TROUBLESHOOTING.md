# Troubleshooting 9Router Portable

## 1. Port 20128 already in use
Another process is occupying port 20128. Close the conflicting application or modify `config/launcher.json`.

## 2. Bundled Node runtime not found
Ensure `runtime/node/node.exe` is present in the portable directory.

## 3. View Logs
Check `logs/launcher.log` and `logs/9router.log` for detailed diagnostics.
