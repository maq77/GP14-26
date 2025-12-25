#!/bin/bash
# Script to help setup GitHub secrets

echo "🔐 GitHub Secrets Setup Helper"
echo "================================"
echo ""
echo "You need to add these secrets to your GitHub repository:"
echo "Settings → Secrets and variables → Actions → New repository secret"
echo ""

cat << 'SECRETS'
Required Secrets:
-----------------

1. CODECOV_TOKEN
   - Get from: https://codecov.io/
   - Used for: Code coverage reports

2. SNYK_TOKEN (optional)
   - Get from: https://snyk.io/
   - Used for: Security scanning

3. SLACK_WEBHOOK_URL (optional)
   - Get from: Slack App settings
   - Used for: Notifications

4. AWS_ACCESS_KEY_ID
5. AWS_SECRET_ACCESS_KEY
6. AWS_REGION
   - Get from: AWS IAM
   - Used for: Deployment to AWS

7. DOCKER_USERNAME (if using Docker Hub)
8. DOCKER_PASSWORD
   - Get from: Docker Hub
   - Used for: Pushing Docker images
9. VITE_API_BASE (URL of your API)
   - Example: https://api.yourdomain.com
   - Used for: Web app to connect to the API
For Production:
---------------
10. AWS_ACCESS_KEY_ID_PROD
11. AWS_SECRET_ACCESS_KEY_PROD
    - Separate credentials for production

12. DATABASE_URL_PROD
13. REDIS_URL_PROD
14. S3_BUCKET_PROD
    - Production environment configs

SECRETS

echo ""
echo "To add secrets via GitHub CLI:"
echo "gh secret set SECRET_NAME"
echo ""