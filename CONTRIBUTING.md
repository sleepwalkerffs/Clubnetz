# Contributing to Clubnetz

Thanks for taking the time to contribute. Bug reports, ideas, translation fixes and pull requests are all welcome.

## Reporting bugs and suggesting features

- Use the [issue forms](https://github.com/sleepwalkerffs/Clubnetz/issues/new/choose). They ask for the details needed to act on a report.
- Search the existing issues first, your topic may already be there.
- **Never put personal data of club members into an issue**, also not in screenshots or logs.
- Security issues don't belong in public issues. See [SECURITY.md](SECURITY.md).

## Pull requests

1. **Open an issue first** for anything bigger than a small fix, so the approach is agreed before you invest time.
2. **Fork** the repository and create a branch from `master`.
3. **Set up the project** as described in the [README](README.md#-getting-started).
4. **Follow the project guidelines** in [src/.github/copilot-instructions.md](src/.github/copilot-instructions.md). The short version:
   - every command and query handler has a test, including that its EF Core query can be translated
   - every user-facing text is localized in English **and** German
   - the UI is designed mobile first and has to look good in light **and** dark mode
   - authorization is resource based, with policies and requirements
   - this is a multi-tenant app: always consider the club (tenant) a request belongs to
5. **Run the tests** (`dotnet test src/Bookennis.sln`).
6. **Open a pull request against `master`** and fill in the template. Keep it focused on one topic.

What happens next:

- The build and the tests run automatically. For first-time contributors a maintainer has to approve the run first.
- A maintainer reviews the pull request. `master` only accepts changes through reviewed pull requests.
- Pull requests are squashed when they are merged, so you don't need to clean up your commits.

The branches `stage` and `production` are deployment branches and are only updated by the maintainers.

## Never commit

- database dumps or any other real member data
- passwords, keys, tokens or connection strings of real environments
- your own imprint or contact details in the settings files

## License of your contribution

Clubnetz is licensed under the [GNU Affero General Public License v3.0](LICENSE). By submitting a contribution you agree that it is licensed under the same license, and you confirm that you have the right to submit it.
