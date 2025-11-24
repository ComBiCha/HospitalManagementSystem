#!/bin/bash

# Exit immediately if a command exits with a non-zero status.
set -e

# Define the Stripe Publishable Key. 
# In a real CI/CD environment, this would typically be loaded from a secure secret management system.
STRIPE_PUBLISHABLE_KEY="pk_test_51S3UfIFkuPFy3lADPYhc1vpWmvExBWlcmlsoommBJlR8041rkardUtKcXxqLcthA7lLiBGEfAYNUegDVsjrSTbLJ00QGFjJ1Rd"

# Navigate to the frontend directory
echo "Navigating to frontend directory..."
cd frontend

# Build the Docker image for the frontend, passing the Stripe Publishable Key as a build argument, and push it to the registry.
echo "Building frontend Docker image..."
docker build -t sangrk/hms-frontend:latest --build-arg NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY=$STRIPE_PUBLISHABLE_KEY . --push

# Navigate back to the root directory
echo "Navigating back to root directory..."
cd ..

# Restart the Kubernetes deployment to pick up the new Docker image.
echo "Restarting Kubernetes deployment..."
kubectl rollout restart deployment/hms-frontend

echo "Frontend deployment process complete."
