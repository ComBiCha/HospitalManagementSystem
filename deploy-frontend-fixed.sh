#!/bin/bash

# Navigate to the frontend directory
cd frontend

echo "🚀 Deploying HMS Frontend with Environment Variables Fix..."

# Install dependencies
echo "📦 Installing dependencies..."
npm install

echo "🔨 Building Next.js application..."
npm run build

echo "🐳 Building Docker image for Linux AMD64..."
docker build --platform linux/amd64 --no-cache -t hms-frontend:latest .

echo "📤 Tagging and pushing to Docker Hub..."
docker tag hms-frontend:latest sangrk/hms-frontend:latest
docker push sangrk/hms-frontend:latest

echo "☸️ Deploying to Kubernetes..."
cd ..
kubectl apply -f hms-frontend-deployment.yaml

echo "⏳ Waiting for deployment to be ready..."
kubectl rollout status deployment/hms-frontend

echo "✅ Frontend deployment completed!"
echo "🌐 Access frontend at: http://localhost:3000 (after port-forward)"

kubectl port-forward svc/hms-frontend-service 3000:80