# MetroDesignToolKits Codex Instructions

## Cache-friendly prompt structure

For every task in this repository, keep this instruction file as the stable prefix. Do not ask the user to restate these rules. Treat the user's latest message as the variable TASK BLOCK at the end.

## Project identity

This repository is MetroDesignToolKits / MetroDesignTools, an AutoCAD secondary development project for metro section generation tooling.

Primary domain:
- AutoCAD .NET plugin development
- C# / WPF / PaletteSet / AutoCAD command lifecycle
- section generation, floor configuration, layer mapping, template management, section update detection

## Required working style

You must be extremely careful and systematic.

Before proposing or editing code:
1. Inspect the repository structure.
2. Read the relevant existing code before changing it.
3. Identify the actual call chain, not just file names.
4. Separate UI interaction issues, backend business logic issues, unfinished tasks, and next-step task planning.
5. Prefer small, reviewable changes.
6. Never invent behavior that is not present in the code.
7. If a feature requires AutoCAD host validation and cannot be verified statically, mark it as "requires real AutoCAD host validation".

## Architecture rules

Follow the existing layered architecture:

- Plugin layer:
  - AutoCAD command entry
  - AutoCAD document/editor/database interactions
  - WPF windows, PaletteSet, host UI orchestration
  - adapter calls into App layer

- App layer:
  - use cases
  - DTO mapping
  - application services
  - validation and orchestration

- Core/Foundation:
  - pure domain models
  - geometry/value objects
  - reusable logic with no AutoCAD dependency

- Infrastructure:
  - DWG persistence
  - repositories
  - AutoCAD-specific implementation details behind interfaces

Do not move business rules into WPF code-behind unless they are purely UI state rules.

## Review checklist

When reviewing this project, always check:

1. UI interaction
   - dirty state
   - save/cancel semantics
   - field validation
   - Palette/window lifecycle
   - state loss during CAD point selection
   - repeated click / busy state
   - error messages and recovery path

2. Backend business logic
   - use case boundaries
   - DTO completeness
   - validation coverage
   - repository read/write consistency
   - snapshot compatibility
   - transaction/rollback behavior
   - hash/update detection consistency

3. AutoCAD integration
   - document lock
   - transaction scope
   - command context
   - modeless window safety
   - selection/prompt cancellation
   - DBObject lifecycle
   - XRecord/XData limits

4. Testing and validation
   - unit tests for pure logic
   - App-layer tests with fake repositories
   - manual AutoCAD host validation for commands and UI
   - regression documentation updates

## Output format

Unless the user asks otherwise, respond in Chinese using this structure:

1. 结论
2. 发现的问题
   - 界面交互问题
   - 后端业务逻辑问题
   - 未闭环任务
3. 建议的下一步任务
4. 可执行的 Codex/开发任务拆分

Be direct. Do not give vague suggestions. For each problem, include:
- affected file or module
- why it is a problem
- recommended fix
- validation method

## Command safety

Before running commands:
- Prefer read-only inspection first.
- Do not run destructive git commands.
- Do not rewrite unrelated files.
- Do not run broad formatting over the whole repository unless explicitly asked.
- If tests cannot run because dependencies or AutoCAD host are missing, state that clearly.

## Git rules

- Keep changes focused.
- Do not mix docs, refactor, feature, and test changes unless requested.
- Use descriptive branch names.
- Use concise commit messages.
- Before final response, summarize changed files and remaining risks.

## TASK BLOCK

The user's latest request below is the only variable part. Apply the stable rules above to it.