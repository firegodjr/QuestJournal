---
id: Tasks
aliases: []
tags: []
---

# TODAY
## MAINQUESTS
	- [>] Custom Text Insight
		- [x] Add caching per-call to generate report based on cacheTTL (if it exists)
			- Added to RunReport. Need to add to KLFetchService though, because it sends nocache every time.
			- [x] Add to KLFetchService
		- [!] Platform-managed long term data
			- Store historical data but not as default
			- Wipe on query change
		- [ ] Generate API keys for insights, only show once
		- [ ] Time series for all schemas?

	- [>] Portal OpenTelemetry
		- [x] Hit up colleague
		- [ ] Grafana Login
		- [ ] Test on dev

	- [>] Miku Portal Issues
		- Errors when completing an UploadTask
		- [x] Investigate
			- 401 Unauthorized from Apps on work item release
		- [ ] Ask about this

## SIDEQUESTS
	- [ ] Schedule Step
		- [ ] Write/generate some general documentation

	- [>] PageIQ & DocumentOS whitelabeling updates
		- [>]  DocumentOS
			- [ ] Updated loading icon colors
		- [>] PageIQ
			- [>] Get final icons for PageIQ

## EPICS
	- [ ] Rewrite the whole thing in Rust
	- [~] Migrate billing service

---

# TOMORROW
## MAINQUESTS
- [>] Portal Pipeline Changes
	1. build whole Portal, not just contracts
	2. push built portal artifact, not just apps
	- [x] Make card for DevOps
	- Tie Portal releases to Apps releases by incorporating Portal build into the main build process
	- [ ] Edit build and push .yaml


## SIDEQUESTS
	- [ ] Portal Login Issues
		- [ ] OTP screen threw an error. How did it get into that state?

	- [ ] Log Portal Bug
		- [ ] PortalTask Management Page "Assigned To" returns concatenated strings

---

# YESTERDAY
## MAINQUESTS
## SIDEQUESTS
