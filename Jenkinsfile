pipeline {
    agent {
        kubernetes {
            yaml """
apiVersion: v1
kind: Pod
metadata:
  labels:
    jenkins: agent
spec:
  serviceAccountName: jenkins-admin
  securityContext:
    fsGroup: 1000
  containers:
  - name: jnlp
    image: jenkins/inbound-agent:latest
    args: ['\$(JENKINS_SECRET)', '\$(JENKINS_NAME)']
    resources:
      requests:
        cpu: "500m"
        memory: "512Mi"
      limits:
        cpu: "1"
        memory: "1Gi"
  
  - name: docker
    image: docker:24.0-dind
    securityContext:
      privileged: true
    env:
    - name: DOCKER_TLS_CERTDIR
      value: ""
    - name: DOCKER_DRIVER
      value: overlay2
    command:
    - dockerd
    - --host=unix:///var/run/docker.sock
    - --host=tcp://0.0.0.0:2375
    - --tls=false
    volumeMounts:
    - name: docker-storage
      mountPath: /var/lib/docker
    - name: docker-sock
      mountPath: /var/run
    resources:
      requests:
        cpu: "2"
        memory: "4Gi"
      limits:
        cpu: "4"
        memory: "8Gi"
    readinessProbe:
      exec:
        command: ["docker", "info"]
      initialDelaySeconds: 20
      periodSeconds: 5
      timeoutSeconds: 10
  
  - name: kubectl
    image: bitnami/kubectl:latest
    command: ['cat']
    tty: true
    resources:
      requests:
        cpu: "250m"
        memory: "256Mi"
      limits:
        cpu: "500m"
        memory: "512Mi"
  
  volumes:
  - name: docker-storage
    emptyDir:
      sizeLimit: 30Gi
  - name: docker-sock
    emptyDir: {}
"""
        }
    }

    options {
        buildDiscarder(logRotator(numToKeepStr: '10'))
        timeout(time: 60, unit: 'MINUTES')
        timestamps()
        disableConcurrentBuilds()
    }

    environment {
        DOCKER_REGISTRY = 'sangrk'
        BACKEND_IMAGE_NAME = "${env.DOCKER_REGISTRY}/hms-api"
        FRONTEND_IMAGE_NAME = "${env.DOCKER_REGISTRY}/hms-frontend"
        IMAGE_TAG = "build-${env.BUILD_NUMBER}"
        DOCKER_HOST = "tcp://localhost:2375"
        DOCKER_BUILDKIT = "1"
    }

    stages {
        stage('Checkout') {
            steps {
                container('jnlp') {
                    echo '🔄 Checking out source code...'
                    checkout scm
                }
            }
        }

        stage('Wait for Docker') {
            steps {
                container('docker') {
                    script {
                        echo '⏳ Waiting for Docker daemon to be ready...'
                        sh '''
                        for i in {1..30}; do
                            if docker info > /dev/null 2>&1; then
                                echo "✅ Docker daemon is ready!"
                                docker info
                                break
                            fi
                            echo "Waiting for Docker daemon... ($i/30)"
                            sleep 3
                        done
                        '''
                    }
                }
            }
        }

        stage('Setup Buildx') {
            steps {
                container('docker') {
                    script {
                        echo '🔧 Setting up Docker Buildx...'
                        sh '''
                        # Remove old builder if exists
                        docker buildx rm mybuilder || true
                        
                        # Create new builder with proper config
                        docker buildx create \\
                            --name mybuilder \\
                            --driver docker-container \\
                            --driver-opt network=host \\
                            --use
                        
                        # Bootstrap the builder
                        docker buildx inspect --bootstrap
                        
                        # Verify
                        docker buildx ls
                        '''
                    }
                }
            }
        }

        stage('Setup Configuration') {
            steps {
                container('kubectl') {
                    script {
                        withCredentials([file(credentialsId: 'hms-env-file', variable: 'ENV_FILE_PATH')]) {
                            echo '⚙️ Applying Kubernetes configurations...'
                            sh """
                            kubectl delete configmap hms-api-config -n default || true
                            kubectl create configmap hms-api-config --from-env-file=\${ENV_FILE_PATH} -n default
                            """
                        }
                    }
                }
            }
        }

        stage('Build & Push Backend') {
            when {
                anyOf {
                    expression { env.BUILD_NUMBER == '1' }
                    changeset "HospitalManagementSystem.API/**"
                }
            }
            steps {
                container('docker') {
                    script {
                        echo "🏗️ Building Backend for linux/amd64: ${env.BACKEND_IMAGE_NAME}:${env.IMAGE_TAG}"
                        
                        withCredentials([usernamePassword(
                            credentialsId: 'dockerhub-credentials',
                            usernameVariable: 'DOCKER_USER',
                            passwordVariable: 'DOCKER_PASS'
                        )]) {
                            sh "echo \${DOCKER_PASS} | docker login -u \${DOCKER_USER} --password-stdin"
                        }
                        
                        sh """
                        docker buildx build \\
                            --platform linux/amd64 \\
                            --tag ${env.BACKEND_IMAGE_NAME}:${env.IMAGE_TAG} \\
                            --tag ${env.BACKEND_IMAGE_NAME}:latest \\
                            --file HospitalManagementSystem.API/Dockerfile \\
                            --progress=plain \\
                            --pull \\
                            --push \\
                            --cache-from type=registry,ref=${env.BACKEND_IMAGE_NAME}:buildcache \\
                            --cache-to type=registry,ref=${env.BACKEND_IMAGE_NAME}:buildcache,mode=max \\
                            .
                        """
                        
                        echo '✅ Backend image built and pushed!'
                    }
                }
            }
        }

        stage('Deploy Backend') {
            when {
                anyOf {
                    expression { env.BUILD_NUMBER == '1' }
                    changeset "HospitalManagementSystem.API/**"
                }
            }
            steps {
                container('kubectl') {
                    script {
                        echo "🚀 Deploying Backend..."
                        sh """
                        kubectl set image deployment/hms-api \\
                            hms-api=${env.BACKEND_IMAGE_NAME}:${env.IMAGE_TAG} \\
                            -n default
                        kubectl rollout status deployment/hms-api -n default --timeout=10m
                        """
                        echo '✅ Backend deployed successfully!'
                    }
                }
            }
        }

        stage('Build & Push Frontend') {
            when {
                anyOf {
                    expression { env.BUILD_NUMBER == '1' }
                    changeset "frontend/**"
                }
            }
            steps {
                container('docker') {
                    script {
                        echo "🏗️ Building Frontend for linux/amd64: ${env.FRONTEND_IMAGE_NAME}:${env.IMAGE_TAG}"
                        
                        withCredentials([usernamePassword(
                            credentialsId: 'dockerhub-credentials',
                            usernameVariable: 'DOCKER_USER',
                            passwordVariable: 'DOCKER_PASS'
                        )]) {
                            sh "echo \${DOCKER_PASS} | docker login -u \${DOCKER_USER} --password-stdin"
                        }
                        
                        dir('frontend') {
                            sh """
                            docker buildx build \\
                                --platform linux/amd64 \\
                                --tag ${env.FRONTEND_IMAGE_NAME}:${env.IMAGE_TAG} \\
                                --tag ${env.FRONTEND_IMAGE_NAME}:latest \\
                                --progress=plain \\
                                --pull \\
                                --push \\
                                --cache-from type=registry,ref=${env.FRONTEND_IMAGE_NAME}:buildcache \\
                                --cache-to type=registry,ref=${env.FRONTEND_IMAGE_NAME}:buildcache,mode=max \\
                                .
                            """
                        }
                        
                        echo '✅ Frontend image built and pushed!'
                    }
                }
            }
        }

        stage('Deploy Frontend') {
            when {
                anyOf {
                    expression { env.BUILD_NUMBER == '1' }
                    changeset "frontend/**"
                }
            }
            steps {
                container('kubectl') {
                    script {
                        echo "🚀 Deploying Frontend..."
                        sh """
                        kubectl set image deployment/hms-frontend \\
                            hms-frontend=${env.FRONTEND_IMAGE_NAME}:${env.IMAGE_TAG} \\
                            -n default
                        kubectl rollout status deployment/hms-frontend -n default --timeout=10m
                        """
                        echo '✅ Frontend deployed successfully!'
                    }
                }
            }
        }

        stage('Cleanup') {
            steps {
                container('docker') {
                    script {
                        echo '🧹 Cleaning up Docker resources...'
                        sh '''
                        # Prune old images
                        docker image prune -af --filter "until=24h" || true
                        
                        # Remove buildx builder to free resources
                        docker buildx rm mybuilder || true
                        
                        # Show disk usage
                        docker system df
                        '''
                    }
                }
            }
        }
    }

    post {
        always {
            container('docker') {
                script {
                    echo '🔒 Logging out from Docker Hub...'
                    sh 'docker logout || true'
                }
            }
        }
        success {
            echo '✅ Pipeline completed successfully!'
        }
        failure {
            echo '❌ Pipeline failed! Check logs above.'
        }
        cleanup {
            echo '🧹 Cleaning up workspace...'
        }
    }
}
