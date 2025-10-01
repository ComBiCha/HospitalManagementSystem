'use client'

import { 
  Calendar, 
  FileText, 
  CreditCard, 
  MessageSquare, 
  Shield, 
  BarChart3,
  Users,
  Stethoscope,
  Database,
  Smartphone
} from 'lucide-react'

export default function Features() {
  const features = [
    {
      icon: Calendar,
      title: 'Quản lý lịch hẹn',
      description: 'Sắp xếp lịch hẹn tự động, nhắc nhở bệnh nhân và bác sĩ',
      color: 'bg-blue-100 text-blue-600'
    },
    {
      icon: FileText,
      title: 'Hồ sơ bệnh án',
      description: 'Lưu trữ và quản lý hồ sơ bệnh án điện tử an toàn',
      color: 'bg-green-100 text-green-600'
    },
    {
      icon: CreditCard,
      title: 'Thanh toán',
      description: 'Xử lý thanh toán trực tuyến và quản lý hóa đơn',
      color: 'bg-purple-100 text-purple-600'
    },
    {
      icon: MessageSquare,
      title: 'Thông báo',
      description: 'Gửi thông báo tự động qua email và SMS',
      color: 'bg-orange-100 text-orange-600'
    },
    {
      icon: Shield,
      title: 'Bảo mật',
      description: 'Mã hóa dữ liệu và xác thực đa lớp bảo vệ thông tin',
      color: 'bg-red-100 text-red-600'
    },
    {
      icon: BarChart3,
      title: 'Báo cáo',
      description: 'Tạo báo cáo thống kê và phân tích dữ liệu',
      color: 'bg-indigo-100 text-indigo-600'
    },
    {
      icon: Users,
      title: 'Quản lý nhân viên',
      description: 'Quản lý thông tin bác sĩ, y tá và nhân viên',
      color: 'bg-pink-100 text-pink-600'
    },
    {
      icon: Stethoscope,
      title: 'Chẩn đoán',
      description: 'Hỗ trợ chẩn đoán với AI và cơ sở dữ liệu y tế',
      color: 'bg-teal-100 text-teal-600'
    },
    {
      icon: Database,
      title: 'Lưu trữ DICOM',
      description: 'Lưu trữ và quản lý hình ảnh y tế DICOM',
      color: 'bg-cyan-100 text-cyan-600'
    },
    {
      icon: Smartphone,
      title: 'Ứng dụng di động',
      description: 'Truy cập hệ thống từ mọi thiết bị di động',
      color: 'bg-yellow-100 text-yellow-600'
    }
  ]

  return (
    <section id="features" className="py-20 bg-white">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="text-center mb-16">
          <h2 className="text-3xl lg:text-4xl font-bold text-gray-900 mb-4">
            Tính năng nổi bật
          </h2>
          <p className="text-xl text-gray-600 max-w-3xl mx-auto">
            Hệ thống quản lý bệnh viện toàn diện với các tính năng hiện đại 
            giúp tối ưu hóa quy trình và nâng cao chất lượng dịch vụ y tế.
          </p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-8">
          {features.map((feature, index) => (
            <div
              key={index}
              className="group bg-white p-6 rounded-xl shadow-lg hover:shadow-xl transition-all duration-300 border border-gray-100 hover:border-primary-200"
            >
              <div className={`w-12 h-12 ${feature.color} rounded-lg flex items-center justify-center mb-4 group-hover:scale-110 transition-transform duration-300`}>
                <feature.icon className="w-6 h-6" />
              </div>
              <h3 className="text-xl font-semibold text-gray-900 mb-3">
                {feature.title}
              </h3>
              <p className="text-gray-600 leading-relaxed">
                {feature.description}
              </p>
            </div>
          ))}
        </div>

        {/* CTA Section */}
        <div className="mt-16 text-center">
          <div className="bg-gradient-to-r from-primary-600 to-secondary-600 rounded-2xl p-8 lg:p-12">
            <h3 className="text-2xl lg:text-3xl font-bold text-white mb-4">
              Sẵn sàng trải nghiệm?
            </h3>
            <p className="text-primary-100 text-lg mb-6 max-w-2xl mx-auto">
              Khám phá tất cả các tính năng mạnh mẽ của hệ thống quản lý bệnh viện
            </p>
            <button className="bg-white text-primary-600 font-semibold px-8 py-3 rounded-lg hover:bg-gray-50 transition-colors duration-200">
              Bắt đầu dùng thử
            </button>
          </div>
        </div>
      </div>
    </section>
  )
}

