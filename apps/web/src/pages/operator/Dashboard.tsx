import React from 'react';
import { Card } from '../../components/ui/Card';
import { AlertCircle, CheckCircle, Clock, TrendingUp } from 'lucide-react';

const stats = [
  { label: 'Open Incidents', value: '12', icon: AlertCircle, color: 'text-red-600' },
  { label: 'Resolved Today', value: '8', icon: CheckCircle, color: 'text-green-600' },
  { label: 'In Progress', value: '5', icon: Clock, color: 'text-yellow-600' },
  { label: 'This Week', value: '45', icon: TrendingUp, color: 'text-blue-600' },
];

export const Dashboard: React.FC = () => {
  return (
    <div>
      <h1 className="text-3xl font-bold text-gray-900 mb-8">Dashboard</h1>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
        {stats.map((stat) => {
          const Icon = stat.icon;
          return (
            <Card key={stat.label}>
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-600">{stat.label}</p>
                  <p className="text-3xl font-bold mt-2">{stat.value}</p>
                </div>
                <Icon size={40} className={stat.color} />
              </div>
            </Card>
          );
        })}
      </div>

      <Card title="Recent Incidents">
        <p className="text-gray-600">No recent incidents</p>
      </Card>
    </div>
  );
};
