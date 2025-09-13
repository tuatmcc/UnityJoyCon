# Repository Guidelines

This Unity project provides Joy‑Con support via a Rust-based native HIDAPI binding and generated C# interop.

## Project Structure & Module Organization
- `Assets/UnityJoycon/` — C# API; `NativeMethods.cs` is generated.
- `Assets/Plugins/<platform>/hidapi.{bundle|so|dll}` — native plugin loaded by Unity.
- `Assets/Scripts/SampleJoyCon.cs` — minimal example/diagnostic.
- `Native/` — Rust workspace; crate `hidapi` builds/links upstream HIDAPI via CMake and generates bindings.
- `Native/hidapi/externals/hidapi` — vendored upstream; do not modify except when updating.
- `unity-hidapi/` — secondary crate; see its own `AGENTS.md`.

## Build, Test, and Development Commands
- Build native + regenerate C# bindings: `cd Native && cargo build --release -p hidapi`
  - Outputs to `Native/target/release` and updates `Assets/UnityJoycon/NativeMethods.cs`.
- Lint: `cd Native && cargo clippy --all-targets -- -D warnings`
- Format: `cd Native && cargo fmt --all`
- Unity run: open the project in Unity (2022+), load `Assets/Scenes/SampleScene.unity`, press Play.

Prereqs: Rust toolchain, CMake, a C compiler, and platform SDKs (macOS: Xcode CLT; Linux: build-essential, pkg-config, libudev; Windows: MSVC).

## Coding Style & Naming Conventions
- Rust: rustfmt defaults, 4-space indent; functions/modules `snake_case`, types `CamelCase`, constants `SCREAMING_SNAKE_CASE`. Keep `unsafe` isolated and never panic across FFI.
- C#: PascalCase for types/methods; fields `_camelCase`. Do not edit `Assets/UnityJoycon/NativeMethods.cs` by hand.

## Testing Guidelines
- Rust: `cargo test -p hidapi`. Hardware-dependent tests should be ignored or feature-gated and skip cleanly without devices.
- Unity: use `SampleScene` with a paired Joy‑Con; log output appears in the Console.

## Commit & Pull Request Guidelines
- Commits: concise, imperative subjects (optionally Conventional Commits). Reference issues (e.g., `Closes #123`).
- PRs: describe motivation, platforms built/tested, and any API changes. Exclude compiled artifacts; avoid touching vendored externals unless updating.

## Agent-Specific Instructions
- Prefer adding glue in Rust or C# layers; avoid invasive changes under `Native/hidapi/externals`.
- Document any build flag or toolchain changes in `Native/hidapi/build.rs` comments and this file.

