# Contributing Guidelines

Thank you for your interest in contributing to Aurum! We welcome contributions from the community to help make FFXIV crafting smarter and more profitable.

## Code of Conduct

Please treat everyone with respect. Harassment, hate speech, or abuse of any kind will not be tolerated.

## How to Contribute

### Reporting Bugs

If you find a bug, please create a new issue on GitHub. Include:
1. A clear title and description.
2. Steps to reproduce the issue.
3. Expected vs. actual behavior.
4. Screenshots or logs (from `Aurum/bin/Debug/aurum.log`) if applicable.
5. Your game version and Dalamud version.

### Suggesting Features

We love new ideas! Please open an issue with the "feature request" label. Describe:
1. The problem you're trying to solve.
2. Your proposed solution.
3. Any alternatives you've considered.

### Pull Requests

1. **Fork** the repository and create a new branch for your feature or fix.
2. **Follow** the [Code Style](#code-style) guidelines.
3. **Test** your changes using the [Manual Testing Checklist](CONTRIBUTING.md#manual-testing-checklist).
4. **Submit** a Pull Request (PR) with a clear description of your changes.
5. **Link** any related issues in your PR description (e.g., "Fixes #123").

## Development Process

1.  **Setup**: Follow the [Development Setup](CONTRIBUTING.md#development-setup) guide to get your environment ready.
2.  **Code**: Implement your changes. Keep PRs focused on a single feature or fix.
3.  **Verify**: Run the project's quality gates (build, lint, test) before submitting.
    -   Build: `dotnet build Aurum.sln --configuration Debug`
    -   Test: `dotnet test` (if applicable)
4.  **Review**: Maintainers will review your PR and provide feedback. Be prepared to make adjustments.

## Code Style

-   **Language**: C# 12 (.NET 10.0).
-   **Formatting**: Use standard C# conventions.
-   **Namespaces**: Use file-scoped namespaces.
-   **Variables**: Prefer `var` when the type is obvious.
-   **Architecture**: Keep UI logic (Windows) separate from business logic (Services).
-   **Comments**: Comment *why*, not *what*. Use XML documentation for public APIs.

## License

By contributing to Aurum, you agree that your contributions will be licensed under the [AGPL-3.0 License](LICENSE.md).
