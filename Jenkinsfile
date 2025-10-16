pipeline {
    agent {
        kubernetes {
            yaml """
apiVersion: v1
kind: Pod
spec:
  serviceAccountName: 'jenkins'
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
    securityContext:
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
    image: lachlanevenson/k8s-kubectl:v1.23.3
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
                container('jnlp') {
                    echo 'Checking out source code...'
                    checkout scm
                }
            }
        }

        stage('Setup Configuration') {
            steps {
                container('kubectl') {
                    script {
                        withCredentials([file(credentialsId: 'hms-env-file', variable: 'ENV_FILE_PATH')]) {
                            echo 'Applying Kubernetes configurations from secret file...'
                            sh "kubectl delete configmap hms-api-config || true"
                            sh "kubectl create configmap hms-api-config --from-env-file=${ENV_FILE_PATH}"
                        }
                    }
                }
            }
        }

        stage('Build & Push Backend') {
            when { anyOf { expression { env.BUILD_NUMBER == '1' }; changeset "HospitalManagementSystem.API/**" } }
            steps {
                container('docker') {
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
                container('kubectl') {
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
                container('docker') {
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
                container('kubectl') {
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
                sh 'docker logout || true'
            }
        }
        failure {
            echo 'Pipeline failed! Check logs for details.'
        }
    }
}
