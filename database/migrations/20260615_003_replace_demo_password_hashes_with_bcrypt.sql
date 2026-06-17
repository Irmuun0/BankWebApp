/*
    Replace development-only demo password placeholders with real BCrypt hashes.

    Demo login password remains: Password@123
    Stored values are BCrypt hashes only; plain passwords are not stored.
*/

UPDATE dbo.users
SET password_hash = N'$2a$12$xC2p9eCM6IyhNaoPwy2axOBb6Cmx56MX/.CZT.bDDy7NMFbazt2Ky'
WHERE username = N'admin'
  AND password_hash = N'$demo_hash_replace_with_real_hash_for_Password@123';

UPDATE dbo.users
SET password_hash = N'$2a$12$P60E31yC2m7bvmp28ygxXeJdDfW/3th9ExAEPP6hq2lgSyQabg1ia'
WHERE username = N'bat'
  AND password_hash = N'$demo_hash_replace_with_real_hash_for_Password@123';

UPDATE dbo.users
SET password_hash = N'$2a$12$trpDqCIwZix00fAozxNyJOVss/OTLyC3GqF8EYJNs4Z8YRgaeeN7y'
WHERE username = N'saruul'
  AND password_hash = N'$demo_hash_replace_with_real_hash_for_Password@123';

UPDATE dbo.users
SET password_hash = N'$2a$12$uxhRNvg4GqLKK6S4zPI/CeipBI3qq50rCBBhOgrS2GU5ErqHu3Cya'
WHERE username = N'temuulen'
  AND password_hash = N'$demo_hash_replace_with_real_hash_for_Password@123';
