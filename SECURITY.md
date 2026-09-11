# Security policy

## Reporting a vulnerability

Please report vulnerabilities privately through GitHub's security advisory
reporting for this repository when it is available. Do not publish secrets,
exploit details, or user data in a public issue. If private advisory reporting
is unavailable, open an issue asking for a private contact channel without
including sensitive details.

The CDN upload endpoint is intended to be deployed behind HTTPS and should use
`UPLOAD_SECRET` when uploads are not meant to be public. Keep the secret in the
deployment environment, not in the mod settings or repository files.

## Supported versions

The latest `main` branch is the only actively maintained development line.
Release support depends on the release notes for that version.
