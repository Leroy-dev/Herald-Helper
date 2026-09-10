# Contributing

This is a personal project that may accept contributions once it reaches a stable public release. For now, the repository is open for reference and learning.

## Development workflow

1. Open a terminal in the repository root (the folder containing `HeraldHelper.slnx`).
2. Build: `dotnet build HeraldHelper.slnx -v minimal`
3. Test: `dotnet test HeraldHelper.slnx --no-build`
4. Before committing, run the same commands locally that CI runs.

## Things to avoid committing

- `cfg.ini`, `*.db`, `edenHeaders.*`, `*.log`, or anything from `tmp/`/`bin/`/`obj/`.
- Session cookies, DPAPI blobs, or real account credentials.

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
