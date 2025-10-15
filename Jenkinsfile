pipeline {
    // Định nghĩa một agent chạy trên Kubernetes với các container công cụ cần thiết
    agent {
        kubernetes {
            // Dùng file yaml để định nghĩa pod agent một cách chi tiết
            yaml """
apiVersion: v1
kind: Pod
spec:
  containers:
  - name: jnlp
    image: jenkins/inbound-agent:3345.v03dee9b_f88fc-1
    args: ['\$(JENKINS_SECRET)', '\$(JENKINS_NAME)']
    resources:
      requests:
        cpu: "512m"
        memory: "512Mi"
      limits:
        cpu: "1024m"
        memory: "1024Mi"
  - name: docker
    image: docker:20.10.7
    command: ['cat']
    tty: true
    privileged: true
    volumeMounts:
      - name: docker-sock
        mountPath: /var/run/docker.sock
    resources:
      requests:
        cpu: "512m"
        memory: "512Mi"
      limits:
        cpu: "1024m"
        memory: "1024Mi"
  - name: kubectl
    image: bitnami/kubectl:latest
    command: ['cat']
    tty: true
    resources:
      requests:
        cpu: "512m"
        memory: "512Mi"
      limits:
        cpu: "1024m"
        memory: "1024Mi"
  volumes:
    - name: docker-sock
      hostPath:
        path: /var/run/docker.sock
"""
        }
    }

    environment {
        DOCKER_REGISTRY = 'sangrk'
        BACKEND_IMAGE_NAME = "${env.DOCKER_REGISTRY}/hms-api"
        FRONTEND_IMAGE_NAME = "${env.DOCKER_REGISTRY}/hms-frontend"
        IMAGE_TAG = "build-${env.BUILD_NUMBER}"
    }

    stages {
        stage('Checkout') {
            steps {
                container('jnlp') { // Chạy trong container mặc định
                    echo 'Checking out source code...'
                    checkout scm
                }
            }
        }

        stage('Setup Configuration') {
            steps {
                container('kubectl') { // Chuyển sang container kubectl
                    echo 'Applying Kubernetes configurations...'
                    sh "kubectl delete configmap hms-api-config || true"
                    sh "kubectl create configmap hms-api-config --from-env-file=.env"
                }
            }
        }

        stage('Build & Push Backend') {
            // Chạy nếu là build đầu tiên HOẶC có thay đổi trong folder backend
            when { anyOf { expression { env.BUILD_NUMBER == '1' }; changeset "HospitalManagementSystem.API/**" } }
            steps {
                container('docker') { // Chuyển sang container docker
                    script {
                        echo "Building Backend Image: ${env.BACKEND_IMAGE_NAME}:${env.IMAGE_TAG}"
                        withCredentials([usernamePassword(credentialsId: 'dockerhub-credentials', usernameVariable: 'DOCKER_USER', passwordVariable: 'DOCKER_PASS')]) {
                            sh "echo ${DOCKER_PASS} | docker login -u ${DOCKER_USER} --password-stdin"
                        }
                        sh """
                        docker buildx build --platform linux/amd64,linux/arm64 \\
                            -t ${env.BACKEND_IMAGE_NAME}:${env.IMAGE_TAG} \\
                            -f HospitalManagementSystem.API/Dockerfile . --push
                        """
                    }
                }
            }
        }

        stage('Deploy Backend') {
            when { anyOf { expression { env.BUILD_NUMBER == '1' }; changeset "HospitalManagementSystem.API/**" } }
            steps {
                container('kubectl') { // Chuyển sang container kubectl
                    script {
                        echo "Deploying new Backend image..."
                        sh "kubectl set image deployment/hms-api hms-api=${env.BACKEND_IMAGE_NAME}:${env.IMAGE_TAG}"
                        sh "kubectl rollout restart deployment/hms-api"
                        sh "kubectl rollout status deployment/hms-api"
                    }
                }
            }
        }

        stage('Build & Push Frontend') {
            when { anyOf { expression { env.BUILD_NUMBER == '1' }; changeset "frontend/**" } }
            steps {
                container('docker') { // Chuyển sang container docker
                    script {
                        echo "Building Frontend Image: ${env.FRONTEND_IMAGE_NAME}:${env.IMAGE_TAG}"
                        withCredentials([usernamePassword(credentialsId: 'dockerhub-credentials', usernameVariable: 'DOCKER_USER', passwordVariable: 'DOCKER_PASS')]) {
                            sh "echo ${DOCKER_PASS} | docker login -u ${DOCKER_USER} --password-stdin"
                        }
                        dir('frontend') {
                            sh "docker build -t ${env.FRONTEND_IMAGE_NAME}:${env.IMAGE_TAG} ."
                            sh "docker push ${env.FRONTEND_IMAGE_NAME}:${env.IMAGE_TAG}"
                        }
                    }
                }
            }
        }

        stage('Deploy Frontend') {
            when { anyOf { expression { env.BUILD_NUMBER == '1' }; changeset "frontend/**" } }
            steps {
                container('kubectl') { // Chuyển sang container kubectl
                    script {
                        echo "Deploying new Frontend image..."
                        sh "kubectl set image deployment/hms-frontend hms-frontend=${env.FRONTEND_IMAGE_NAME}:${env.IMAGE_TAG}"
                        sh "kubectl rollout restart deployment/hms-frontend"
                        sh "kubectl rollout status deployment/hms-frontend"
                    }
                }
            }
        }
    }

    post {
        always {
            container('docker') {
                echo 'Logging out from Docker Hub...'
                sh 'docker logout'
            }
        }
    }
}
