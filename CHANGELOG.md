# Changelog

[中文](CHANGELOG.zh-CN.md)

All notable changes to this repository will be documented in this file.

The project is in pre-release open-source extraction. Until the first tagged release, entries focus on repository-shaping milestones.

## Unreleased

- Fixed Windows local artifact URLs to use the standard three-slash `file:///C:/...` form instead of the invalid two-slash `file://C:/...` form, which placed the drive letter in the URL authority and could not be opened by hosts. Linux/macOS URLs were already standard-compliant and are unchanged.
- Established bilingual repository execution rules in `AGENTS.md` and `AGENTS.zh-CN.md`.
- Added flagship bilingual landing pages in `README.md` and `README.zh-CN.md`.
- Started the public documentation tree and authored AO/SO guide source documents.
