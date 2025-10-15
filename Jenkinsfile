pipeline {
    agent any

    environment {
        DOCKER_REGISTRY = 'sangrk' // Thay bằng tên Docker Hub registry của bạn
        BACKEND_IMAGE_NAME = "${env.DOCKER_REGISTRY}/hms-api"
        FRONTEND_IMAGE_NAME = "${env.DOCKER_REGISTRY}/hms-frontend"
        // Dùng Build Number của Jenkins để tạo tag duy nhất cho mỗi lần build
        IMAGE_TAG = "build-${env.BUILD_NUMBER}"
    }

    stages {
        stage('Checkout') {
            steps {
                // Lấy code từ GitHub
                checkout scm
            }
        }

        // --- STAGE CHO BACKEND ---
        stage('Build & Push Backend') {
            // Chỉ chạy stage này nếu có thay đổi trong folder backend
            when {
                changeset "HospitalManagementSystem.API/**"
            }
            steps {
                script {
                    echo "Building Backend Image: ${env.BACKEND_IMAGE_NAME}:${env.IMAGE_TAG}"
                    // Đăng nhập vào Docker Hub (sử dụng credentials đã lưu trong Jenkins)
                    withCredentials([usernamePassword(credentialsId: 'dockerhub-credentials', usernameVariable: 'DOCKER_USER', passwordVariable: 'DOCKER_PASS')]) {
                        sh "echo ${DOCKER_PASS} | docker login -u ${DOCKER_USER} --password-stdin"
                    }

                    // Build và push multi-platform image
                    sh """
                    docker buildx build --platform linux/amd64,linux/arm64 \\
                        -t ${env.BACKEND_IMAGE_NAME}:${env.IMAGE_TAG} \\
                        -f HospitalManagementSystem.API/Dockerfile . --push
                    """
                }
            }
        }

        stage('Deploy Backend') {
            when {
                changeset "HospitalManagementSystem.API/**"
            }
            steps {
                script {
                    echo "Deploying new Backend image..."
                    // Dùng image mới để cập nhật deployment
                    sh "kubectl set image deployment/hms-api hms-api=${env.BACKEND_IMAGE_NAME}:${env.IMAGE_TAG}"
                    // Khởi động lại deployment để áp dụng thay đổi
                    sh "kubectl rollout restart deployment/hms-api"
                    sh "kubectl rollout status deployment/hms-api"
                }
            }
        }

        // --- STAGE CHO FRONTEND ---
        stage('Build & Push Frontend') {
            // Chỉ chạy stage này nếu có thay đổi trong folder frontend
            when {
                changeset "frontend/**"
            }
            steps {
                script {
                    echo "Building Frontend Image: ${env.FRONTEND_IMAGE_NAME}:${env.IMAGE_TAG}"
                    // Đăng nhập Docker Hub
                    withCredentials([usernamePassword(credentialsId: 'dockerhub-credentials', usernameVariable: 'DOCKER_USER', passwordVariable: 'DOCKER_PASS')]) {
                        sh "echo ${DOCKER_PASS} | docker login -u ${DOCKER_USER} --password-stdin"
                    }

                    // Build và push image (nên dùng buildx cho cả frontend)
                    // Chạy build từ trong folder frontend
                    dir('frontend') {
                        sh "docker build -t ${env.FRONTEND_IMAGE_NAME}:${env.IMAGE_TAG} ."
                        sh "docker push ${env.FRONTEND_IMAGE_NAME}:${env.IMAGE_TAG}"
                    }
                }
            }
        }

        stage('Deploy Frontend') {
            when {
                changeset "frontend/**"
            }
            steps {
                script {
                    echo "Deploying new Frontend image..."
                    sh "kubectl set image deployment/hms-frontend hms-frontend=${env.FRONTEND_IMAGE_NAME}:${env.IMAGE_TAG}"
                    sh "kubectl rollout restart deployment/hms-frontend"
                    sh "kubectl rollout status deployment/hms-frontend"
                }
            }
        }
    }

    post {
        always {
            // Đăng xuất Docker Hub sau khi pipeline hoàn thành
            echo 'Logging out from Docker Hub...'
            sh 'docker logout'
        }
    }
}
