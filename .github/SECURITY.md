# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.x.x   | :white_check_mark: |

## Reporting a Vulnerability

If you discover a security vulnerability in Nebula Panel, please report it responsibly:

1. **Do NOT** open a public issue
2. Email security concerns to the maintainer via GitHub private message
3. Include as much detail as possible:
   - Description of the vulnerability
   - Steps to reproduce
   - Potential impact
   - Suggested fix (if any)

## Response Timeline

- **Acknowledgment**: Within 48 hours
- **Initial Assessment**: Within 1 week
- **Fix Timeline**: Depends on severity, typically within 30 days for critical issues

## Security Best Practices for Deployment

- Always run Nebula Panel behind a reverse proxy (nginx, Caddy, etc.)
- Use HTTPS in production
- Keep your installation updated
- Use strong passwords for all accounts
- Restrict network access to the management interface
- Regularly backup your data and configuration
