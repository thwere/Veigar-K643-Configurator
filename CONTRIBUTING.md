CONTRIBUTING TO VEIGAR K643 CONFIGURATOR

Copyright (C) 2026 thwere

Thank you for your interest in the project. The software is currently in an early experimental stage. Contribution rules may evolve as the codebase and development workflow take shape.

BEFORE MAKING A CHANGE
- For a substantial feature or architectural change, open an Issue first to discuss the approach.
- Keep changes focused. Avoid combining unrelated fixes and features in one contribution.
- Do not submit undocumented or unverified USB/HID commands as known-safe behaviour.
- Do not include passwords, personal text, private device identifiers, or other sensitive information in logs or issue attachments.

CODE
- The primary language is C#.
- Follow the repository .editorconfig settings.
- Keep device-specific protocol code separate from application UI and general lighting logic.
- Prefer small, focused classes and files with clear responsibilities.
- Add or update automated tests when practical.
- Explain any hardware-specific assumptions and how they were verified.

PULL REQUESTS
- Describe the problem and the proposed change.
- List the steps used to test the change.
- Identify any hardware and Windows versions used for testing.
- Clearly mark behaviour that is experimental or not verified on a physical keyboard.

LICENSE
By submitting a contribution intended for inclusion in this repository, you agree that it will be distributed under the project's GNU GPL-3.0-or-later license, unless a separate written agreement is made with the maintainers.

The project maintainer may review, request changes to, or decline contributions. These guidelines are initial and may be revised.
