# 08: Soak: `/agent soak <char> <n>`

**What to build:** An admin runs `/agent soak <char> <n>` and the Agent does n full login/logout cycles one after another. Each cycle is checked to reach in-world play and then fully disconnect. After the run, the online client count and session allocation are compared against their values before the run. The command stops at the first failing cycle and reports pass or fail, the failing cycle's number and the reason. This is milestone 1's repeatable acceptance test. See the spec.

**Blocked by:** 04, 05

**Status:** ready-for-agent

- [ ] `/agent soak <char> <n>` is admin-only and runs cycles sequentially, never in parallel
- [ ] A cycle that doesn't reach in-world play, or doesn't fully disconnect, counts as a failure
- [ ] Leaked clients or sessions after the run are reported as a failure
- [ ] The first failure stops the run and reports the cycle number and reason
- [ ] `/agent soak <char> 50` passes with a clean server log (milestone 1 acceptance)
