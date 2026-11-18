# Open Risks Register — PVC Studio Pro

**Document Version:** 1.0.0  
**Date:** 2026-11-18  
**Risk Scoring:** Low / Medium / High / Critical (Severity × Likelihood)  

---

## 1. Risk Matrix

| Risk ID | Risk Description | Category | Severity | Likelihood | Impact | Mitigation Strategy | Owner |
|---|---|---|---|---|---|---|---|
| **RSK-01** | **Epson Driver CD/DVD Tray Feed Differences** | Hardware | **High** | High | Card misprint, wasted plastic blanks | Implemented dynamic Win32 `DeviceCapabilities` inspection and 2-step millimeter calibration wizard with plain-paper test before PVC printing. | Printing Lead |
| **RSK-02** | **UIDAI PDF Layout Updates** | Data/Template | **High** | Medium | Bounding box misalignments | Replaced hardcoded coordinates with versioned JSON templates (`templates/v1/*.json`) + keyword anchors. Users can update templates without recompiling. | Core Architect |
| **RSK-03** | **Memory Residuals & DPDP Compliance** | Security | **Critical** | Low | Data leakage audit failure | Adopted **Zero-PII Storage Policy**: citizen names, Aadhaar numbers, and images are 100% ephemeral and discarded immediately. Buffers zeroized with `SecureZeroMemory`. | Security Lead |
| **RSK-04** | **Complex Background AI Segmentation Edge Cases** | AI/Vision | **Medium** | Medium | Imperfect hair cutouts on noisy shop walls | Keep AI background removal strictly optional with interactive brush/eraser touchup controls; default fallback to solid crop. | AI/CV Lead |
| **RSK-05** | **Physical Hardware Testing Absence in Development** | QA | **High** | High | Inaccurate margin assumptions | Strictly separate `SPOOLER_VERIFIED` from `PHYSICAL_HARDWARE_VALIDATED`. User validates on real shop Epson L805 before production sign-off. | QA Lead |
| **RSK-06** | **.NET Runtime Dependencies on Client PCs** | Deployment | **Medium** | Low | App fails to launch on clean Windows 11 | Build self-contained deployment (`--self-contained true`) bundled into an Inno Setup installer so no pre-installed .NET runtime is required. | Release Lead |
