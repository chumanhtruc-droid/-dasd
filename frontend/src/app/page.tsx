"use client";
import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { motion } from 'framer-motion';
import { KeyRound, ArrowRight } from 'lucide-react';
import toast from 'react-hot-toast';

export default function Home() {
  const [key, setKey] = useState('');
  const router = useRouter();

  const handleJoin = (e: React.FormEvent) => {
    e.preventDefault();
    if (!key.trim()) return toast.error("Vui lòng nhập Key!");
    // In a real app we might verify key via API first
    router.push(`/chat?key=${encodeURIComponent(key)}`);
  };

  return (
    <main className="flex min-h-screen flex-col items-center justify-center bg-[#0a0a0a] p-4 relative overflow-hidden">
      {/* Decorative background blur */}
      <div className="absolute top-1/4 left-1/4 w-96 h-96 bg-blue-600/20 rounded-full blur-[128px]" />
      
      <motion.div 
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.5 }}
        className="w-full max-w-md bg-[#111111]/80 backdrop-blur-xl p-8 rounded-3xl border border-gray-800 shadow-2xl relative z-10"
      >
        <div className="flex justify-center mb-8">
          <div className="p-4 bg-blue-500/10 rounded-2xl">
            <KeyRound className="w-12 h-12 text-blue-500" />
          </div>
        </div>
        
        <h1 className="text-3xl font-bold text-center text-white mb-2">Kết nối Chat</h1>
        <p className="text-gray-400 text-center mb-8">Nhập Connection Key do Admin cung cấp</p>

        <form onSubmit={handleJoin} className="space-y-6">
          <div>
            <input
              type="text"
              value={key}
              onChange={(e) => setKey(e.target.value)}
              placeholder="VD: ABCD-1234-EFGH"
              className="w-full px-5 py-4 bg-[#1a1a1a] border border-gray-700 rounded-xl text-white placeholder-gray-500 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500 transition-all font-mono text-center tracking-widest uppercase"
            />
          </div>
          
          <button
            type="submit"
            className="w-full py-4 px-6 bg-blue-600 hover:bg-blue-500 text-white rounded-xl font-medium transition-all flex items-center justify-center gap-2 group"
          >
            Vào phòng
            <ArrowRight className="w-5 h-5 group-hover:translate-x-1 transition-transform" />
          </button>
        </form>
      </motion.div>
    </main>
  );
}
