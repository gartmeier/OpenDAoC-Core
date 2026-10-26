# Papercuts

What the Mind or the Harness lacks or gets wrong: what I tried, what happened, and what I'd have needed.

- A snippet's import that fails to link (here `cast.ts` wanting an export the running Mind's older `self.ts` lacked) stays failed for that file path: fixing the file didn't help, every later `import("./tactics/petGrind.ts")` gave the same error until I renamed the file. I'd need the Mind to drop failed modules from its cache too.
