# 9Router Portable Windows — Architecture & Implementation

## Overview
`9Router Portable` is a Windows x64 desktop launcher for `9Router` (`decolua/9router`). It bundles Node.js, the upstream 9Router standalone build, and a high-performance C# WinForms controller.

## Core Features
- **Zero Global Dependencies**: No Node, npm, WSL, or Docker required on target machine.
- **Strict Portability**: All persistent configuration, database, secrets, and logs live inside `data/` adjacent to `9RouterPortable.exe`.
- **Process Management**: Windows Job Object (`JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`) guarantees zero orphaned `node.exe` processes upon exit.
- **System Tray Integration**: Minimizes to tray, supports tray context menu.
- **Health Checks & Recovery**: Automated HTTP/TCP health checking with crash detection.
