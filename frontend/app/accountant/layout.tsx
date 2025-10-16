'use client';

import { useState } from 'react';
import { LayoutDashboard, Users, Settings, CreditCard } from 'lucide-react';

export default function AccountantLayout({ children }: { children: React.ReactNode }) {
  const [activeSection, setActiveSection] = useState('payments');

  const menuItems = [
    { id: 'payments', label: 'Thu phí', icon: CreditCard },
    { id: 'reports', label: 'Báo cáo', icon: LayoutDashboard }//,
    // { id: 'settings', label: 'Cài đặt', icon: Settings },
  ];

  return (
    <div className="flex min-h-screen bg-gray-50">
      <aside className="w-64 bg-white border-r border-gray-200 p-4 space-y-2">
        <h2 className="text-xl font-bold text-gray-800 mb-4">Cổng kế toán</h2>
        <nav>
          <ul>
            {menuItems.map((item) => (
              <li key={item.id}>
                <a
                  href="#"
                  onClick={() => setActiveSection(item.id)}
                  className={`flex items-center px-4 py-2.5 rounded-lg text-sm font-medium transition-colors ${
                    activeSection === item.id
                      ? 'bg-blue-600 text-white shadow-md'
                      : 'text-gray-600 hover:bg-gray-100'
                  }`}>
                  <item.icon className="w-5 h-5 mr-3" />
                  {item.label}
                </a>
              </li>
            ))}
          </ul>
        </nav>
      </aside>

      <main className="flex-1 p-8">
        {children}
      </main>
    </div>
  );
}
