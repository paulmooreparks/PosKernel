# Contributing to POS Kernel

Thank you for your interest in contributing to POS Kernel! This document outlines the process for contributing to this project.

## License Agreement

By contributing to POS Kernel, you agree that your contributions will be licensed under the Apache License 2.0, the same license that covers the project. You retain copyright ownership of your contributions.

## Contributor License Agreement (CLA)

For significant contributions, we may require a Contributor License Agreement to ensure the project can continue to be distributed under the Apache License 2.0. This will be discussed on a case-by-case basis.

## How to Contribute

### Reporting Issues

1. Check existing issues to avoid duplicates
2. Use the issue templates when available
3. Include relevant system information (OS, .NET version, Rust version)
4. Provide clear reproduction steps for bugs

### Submitting Changes

1. Fork the repository
2. Create a feature branch from `master`
3. Make your changes following the coding standards below
4. Add tests for new functionality
5. Ensure all tests pass
6. Submit a pull request

### Coding Standards

#### Rust Code
- Follow standard Rust formatting (`cargo fmt`)
- Use `clippy` for linting (`cargo clippy`)
- Include appropriate documentation comments
- Maintain the existing error handling patterns
- Add Apache License header to new files

#### C# Code
- Follow Microsoft C# coding conventions
- Use nullable reference types appropriately
- Include XML documentation comments
- Maintain consistent exception handling
- Add Apache License header to new files

#### Documentation
- Update relevant documentation for changes
- Include examples for new features
- Maintain the existing documentation structure

## Development Setup

See the main README.md for build instructions.

## Questions?

If you have questions about contributing, please open a GitHub Discussion or contact the maintainers.

---

**Copyright 2025 Paul Moore Parks and contributors**  
Licensed under the Apache License, Version 2.0
