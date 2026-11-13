# Network measurements

Generated from recorded CSV only. N/A is missing data, never zero. p95 uses nearest rank. Rate means are weighted by actual interval duration; p95 uses interval samples. Input latency uses all events from companion *_input.csv (CPU LateUpdate proxy, not display hardware latency).

Mirror bytes exclude transport headers. Transport callbacks count payload batches, not UDP datagrams/retransmissions. UDP packets and observed loss remain N/A without another measurement source. Host Mirror totals include local loopback; do not divide them by player count to claim per-client traffic.

**PENDING: no measurement files. Stage 2 and NFR-03 are not accepted.**
