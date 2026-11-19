# Reverse-Engineering Skill Discovery, Evaluation & Installation Audit

**Date:** 2026-11-19  
**Branch:** `competitor-feature-parity`  
**Auditor:** Senior Windows Desktop Software Architect  

---

## 1. Skill Discovery & Evaluation Matrix

In accordance with Section 1.1, the Skills CLI and available repositories were inspected:

| Skill / Package Candidate | Source | Evaluation & Code Inspection | Decision |
| :--- | :--- | :--- | :--- |
| **REA (Reverse Engineer Anything)** | `https://github.com/morluto/rea` | Official multi-target reverse-engineering engine for native, managed, PE binaries, GDB/Rizin debugging, and Windows GUI scenarios. Verified source code, strict security boundaries, no hostile scripts. | **INSTALLED & ADOPTED** (Workspace: `.agents/skills/reverse-engineer-anything/`) |
| **pvc-application-reverse-engineering** | Dedicated Project Skill | Tailored specifically for Windows desktop printing applications, menu inventories, coordinate extraction, and verification taxonomies. Strict prohibitions against unverified claims and PII leakage. | **CREATED** (`.agents/skills/pvc-application-reverse-engineering/`) |
| **pvc-document-print-engine** | Dedicated Project Skill | Tailored for physical millimeter geometry, single source of truth between preview and spooler, driver printable margins, and CR80 fidelity. | **CREATED** (`.agents/skills/pvc-document-print-engine/`) |
| `reverse-browser-automation` | `sickn33/agentic-awesome-skills` | Focused on browser web automation; not applicable to Windows GDI/WPF native applications. | Rejected |
| `electron-reverse-engineering` | `yunusgungor` | Tailored for Node/Electron asar packages; reference software is .NET WinForms. | Rejected |

---

## 2. Active Invocation Confirmation

- **Primary Investigation Skill:** `pvc-application-reverse-engineering` is explicitly invoked for Section 3 (Reinspect PVC Card Pro) and Section 4 (Card Studio Aadhaar Root Cause).
- **Physical Layout & Printing Skill:** `pvc-document-print-engine` is explicitly invoked for Section 5 (Document Page Designer) and Section 6 (True Print Preview).
- **Binary/Decompilation Engine:** `reverse-engineer-anything` (REA) is active for managed metadata and PE resource validation.
