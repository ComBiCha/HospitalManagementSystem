#!/bin/bash

# Navigate to project root (nếu cần)
# cd /Users/tranhothanhsang/Projects/Net_Intern/Hospital_Management_System

API_VERSION=${1:-v4}
API_IMAGE=sangrk/hms-api:$API_VERSION
API_DOCKERFILE=HospitalManagementSystem.API/Dockerfile
API_CONTEXT=.
API_DEPLOYMENT_YAML=hms-api-deployment.yaml

echo "🚀 Deploying HMS API Backend (version: $API_VERSION) ..."

echo "🔨 Building and pushing Docker image for Linux AMD64..."
docker buildx build --platform linux/amd64 -t $API_IMAGE -f $API_DOCKERFILE $API_CONTEXT --push

echo "☸️ Deploying to Kubernetes..."
kubectl apply -f $API_DEPLOYMENT_YAML

echo "⏳ Waiting for deployment to be ready..."
kubectl rollout status deployment/hms-api

echo "✅ API deployment completed!"
