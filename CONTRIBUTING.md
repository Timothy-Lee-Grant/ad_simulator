# Contributing to Ad Simulator

Thank you for your interest in contributing to the Ad Simulator project!

## Getting Started

1. Fork the repository
2. Clone your fork: `git clone https://github.com/YOUR_USERNAME/ad_simulator.git`
3. Create a feature branch: `git checkout -b feature/your-feature-name`
4. Read the documentation in `docs/` folder
5. Follow the implementation guide in `docs/02_STEP_BY_STEP_IMPLEMENTATION_GUIDE.md`

## Development Setup

See `docs/02_STEP_BY_STEP_IMPLEMENTATION_GUIDE.md` Phase 1 for detailed setup instructions.

### Prerequisites
- .NET 8 SDK
- Docker & Docker Compose
- PostgreSQL 15
- Redis 7
- Apache Kafka 3.5+

### Quick Start
```bash
# Clone the repository
git clone https://github.com/Timothy-Lee-Grant/ad_simulator.git
cd ad_simulator

# Start services with Docker Compose
docker-compose up -d

# Build the project
dotnet build

# Run tests
dotnet test
```

## Code Standards

- Follow C# coding standards ([Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions))
- Use meaningful variable and method names
- Add comments for complex logic
- Write unit tests for new features
- Keep methods focused on single responsibility

## Testing

- Unit tests: `dotnet test --filter "Category=Unit"`
- Integration tests: `dotnet test --filter "Category=Integration"`
- All tests: `dotnet test`

## Documentation

- Update relevant documentation files in `docs/` when making changes
- Keep API documentation in sync with endpoint changes
- Update architecture diagrams if you modify system design

## Pull Request Process

1. Update documentation if needed
2. Add/update tests for your changes
3. Ensure all tests pass: `dotnet test`
4. Create a descriptive pull request with:
   - Clear title and description
   - Reference to any related issues
   - Summary of changes
5. Address code review feedback

## Reporting Issues

Use GitHub Issues to report bugs or suggest features. Include:
- Clear description of the issue
- Steps to reproduce (for bugs)
- Expected vs. actual behavior
- Environment details (OS, .NET version, etc.)

## Questions?

- Check the documentation in `docs/` folder
- Review the API documentation in `docs/03_API_DOCUMENTATION.md`
- Check existing issues and discussions

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

Thank you for contributing! 🚀
