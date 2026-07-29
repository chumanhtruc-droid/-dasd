"use client";
import { useEffect, useState, useRef, Suspense } from 'react';
import { useSearchParams } from 'next/navigation';
import { io, Socket } from 'socket.io-client';
import { motion, AnimatePresence } from 'framer-motion';
import { Send, Image as ImageIcon, Check, CheckCheck, Loader2, X } from 'lucide-react';
import toast from 'react-hot-toast';
import axios from 'axios';

type Message = {
  id: string;
  content: string;
  sender: string;
  type: string;
  status: string;
  createdAt: string;
};

function ChatContent() {
  const searchParams = useSearchParams();
  const keyString = searchParams.get('key');
  
  const [socket, setSocket] = useState<Socket | null>(null);
  const [messages, setMessages] = useState<Message[]>([]);
  const [input, setInput] = useState('');
  const [partnerStatus, setPartnerStatus] = useState('OFFLINE');
  const [isUploading, setIsUploading] = useState(false);
  const [previewImage, setPreviewImage] = useState<string | null>(null);
  
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  useEffect(() => {
    if (!keyString) return;

    const newSocket = io('https://dasd-fzft.onrender.com');
    setSocket(newSocket);

    newSocket.on('connect', () => {
      newSocket.emit('join_room', { keyString, sender: 'USER2' });
    });

    newSocket.on('joined', () => {
      toast.success("Đã kết nối vào phòng!");
    });

    newSocket.on('user_status', (data) => {
      if (data.sender !== 'USER2') {
        setPartnerStatus(data.status);
        if (data.status === 'ONLINE') toast("Đối tác đã online", { icon: "👋" });
      }
    });

    newSocket.on('new_message', (msg: Message) => {
      setMessages(prev => [...prev, msg]);
      
      // If it's an image from partner, play sound
      if (msg.type === 'IMAGE' && msg.sender !== 'USER2') {
        toast.success("Đã nhận một ảnh mới!");
        // Play notification sound
        const audio = new Audio('/notification.mp3');
        audio.play().catch(e => console.log("Audio play failed"));
      }
    });

    return () => {
      newSocket.disconnect();
    };
  }, [keyString]);

  const sendMessage = (content: string, type: string = 'TEXT') => {
    if (!socket || !content.trim()) return;
    socket.emit('send_message', { content, type });
    if (type === 'TEXT') setInput('');
  };

  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setIsUploading(true);
    const formData = new FormData();
    formData.append('file', file);

    try {
      const res = await axios.post('https://dasd-fzft.onrender.com/api/upload', formData);
      if (res.data.url) {
        sendMessage(res.data.url, 'IMAGE');
      }
    } catch (error) {
      toast.error("Upload thất bại!");
    } finally {
      setIsUploading(false);
    }
  };

  return (
    <div className="flex flex-col h-screen bg-[#0a0a0a] text-gray-200">
      {/* Header */}
      <header className="px-6 py-4 bg-[#111111] border-b border-gray-800 flex items-center justify-between">
        <div className="flex items-center gap-4">
          <div className="w-10 h-10 bg-blue-600 rounded-full flex items-center justify-center font-bold text-white shadow-lg">
            W
          </div>
          <div>
            <h2 className="font-semibold text-white">Windows Client</h2>
            <div className="flex items-center gap-2 text-xs">
              <span className={`w-2 h-2 rounded-full ${partnerStatus === 'ONLINE' ? 'bg-green-500 shadow-[0_0_8px_#22c55e]' : 'bg-gray-500'}`} />
              <span className={partnerStatus === 'ONLINE' ? 'text-green-500' : 'text-gray-500'}>
                {partnerStatus === 'ONLINE' ? 'Online' : 'Offline'}
              </span>
            </div>
          </div>
        </div>
      </header>

      {/* Messages */}
      <div className="flex-1 overflow-y-auto p-6 space-y-6">
        <AnimatePresence>
          {messages.map((msg, i) => {
            const isMe = msg.sender === 'USER2';
            return (
              <motion.div
                key={msg.id || i}
                initial={{ opacity: 0, y: 10 }}
                animate={{ opacity: 1, y: 0 }}
                className={`flex flex-col ${isMe ? 'items-end' : 'items-start'}`}
              >
                <div className={`max-w-[70%] p-4 rounded-2xl ${
                  isMe ? 'bg-blue-600 text-white rounded-br-sm' : 'bg-[#1a1a1a] border border-gray-800 text-gray-100 rounded-bl-sm'
                }`}>
                  {msg.type === 'IMAGE' ? (
                    <img 
                      src={msg.content} 
                      alt="Shared content" 
                      className="rounded-lg max-w-full cursor-zoom-in hover:opacity-90 transition-opacity" 
                      onClick={() => setPreviewImage(msg.content)}
                    />
                  ) : (
                    <p className="whitespace-pre-wrap">{msg.content}</p>
                  )}
                </div>
                <span className="text-[10px] text-gray-500 mt-1 flex items-center gap-1">
                  {new Date(msg.createdAt).toLocaleTimeString()}
                  {isMe && (
                    msg.status === 'READ' ? <CheckCheck className="w-3 h-3 text-blue-400" /> : <Check className="w-3 h-3" />
                  )}
                </span>
              </motion.div>
            );
          })}
        </AnimatePresence>
        <div ref={messagesEndRef} />
      </div>

      {/* Input */}
      <div className="p-4 bg-[#111111] border-t border-gray-800">
        <div className="max-w-4xl mx-auto flex items-center gap-3">
          <input
            type="file"
            ref={fileInputRef}
            className="hidden"
            accept="image/*"
            onChange={handleFileUpload}
          />
          <button 
            className="p-3 text-gray-400 hover:text-white hover:bg-gray-800 rounded-full transition-colors"
            onClick={() => fileInputRef.current?.click()}
            disabled={isUploading}
          >
            {isUploading ? <Loader2 className="w-5 h-5 animate-spin" /> : <ImageIcon className="w-5 h-5" />}
          </button>
          <div className="flex-1 relative">
            <input
              type="text"
              value={input}
              onChange={(e) => setInput(e.target.value)}
              onKeyDown={(e) => e.key === 'Enter' && sendMessage(input)}
              placeholder="Nhập tin nhắn..."
              className="w-full px-5 py-3 bg-[#1a1a1a] border border-gray-700 rounded-full text-white placeholder-gray-500 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500 transition-all pr-12"
            />
          </div>
          <button
            onClick={() => sendMessage(input)}
            disabled={!input.trim()}
            className="p-3 bg-blue-600 hover:bg-blue-500 disabled:bg-gray-800 disabled:text-gray-500 text-white rounded-full transition-colors"
          >
            <Send className="w-5 h-5" />
          </button>
        </div>
      </div>

      {/* Image Preview Modal */}
      <AnimatePresence>
        {previewImage && (
          <motion.div 
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-50 flex items-center justify-center bg-black/90 p-4"
          >
            <button 
              className="absolute top-6 right-6 p-2 bg-gray-800 text-white rounded-full hover:bg-gray-700 transition"
              onClick={() => setPreviewImage(null)}
            >
              <X className="w-6 h-6" />
            </button>
            <img src={previewImage} className="max-w-full max-h-[90vh] object-contain rounded-xl shadow-2xl" />
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

export default function ChatPage() {
  return (
    <Suspense fallback={<div className="flex h-screen items-center justify-center bg-[#0a0a0a] text-white">Đang tải...</div>}>
      <ChatContent />
    </Suspense>
  );
}
