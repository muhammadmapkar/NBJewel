<!-- Role: Explains configuration and operation of protected platform data protection keys. -->
# Platform data protection keys

Set `AI_DATA_PROTECTION_SECRET` as a base64-encoded 32-byte random secret using backend environment variables or user secrets. Set `AI_PLATFORM_CONNECTION` to the platform SQL connection, or use the existing `NBJEWEL_IDENTITY_CONNECTION` fallback. Never commit either value. All instances sharing protected data need the same secret, SQL database, and application name.

The platform schema includes `AiDataProtectionKeys`. Apply the schema through the project's normal database provisioning process before startup. The application stores the ASP.NET Core Data Protection key ring in that table encrypted with AES-GCM. Startup fails if the secret is malformed, SQL is unavailable, or stored key records cannot be authenticated. Back up the database and secret separately. Losing or changing the secret makes existing history and sessions unreadable; restoring the original secret and database backup is required for recovery.

In Development only, an absent secret selects an ephemeral provider and emits a warning. Its keys are not restart durable, so sessions and encrypted history cannot be recovered after restart. Production requires both a secret and SQL connection. No key directory is needed.
