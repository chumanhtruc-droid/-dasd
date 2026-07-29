"use client";
import { useEffect, useState, useRef, Suspense } from 'react';
import { useSearchParams } from 'next/navigation';
import { io, Socket } from 'socket.io-client';
import { motion, AnimatePresence } from 'framer-motion';
import { Send, Image as ImageIcon, Check, CheckCheck, Loader2, X, Download } from 'lucide-react';
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
  const [pastInput, setPastInput] = useState('');
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

    const newSocket = io('https://dasd-1z1t.onrender.com');
    setSocket(newSocket);

    newSocket.on('connect', () => {
      newSocket.emit('join_room', { keyString, sender: 'USER2' });
    });

    newSocket.on('joined', () => {
      toast.success("Đã kết nối vào phòng!");
    });
    
    newSocket.on('clear_chat', () => {
      setMessages([]);
      toast("Lịch sử trò chuyện đã được xóa", { icon: "🧹" });
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

  const sendPastMessage = () => {
    if (!socket || !pastInput.trim()) return;
    socket.emit('send_message', { content: pastInput, type: 'PASTE_TEXT' });
    setPastInput('');
    toast.success("Đã gửi nội dung Past!");
  };

  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setIsUploading(true);
    const formData = new FormData();
    formData.append('file', file);

    try {
      const res = await axios.post('https://dasd-1z1t.onrender.com/api/upload', formData);
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
        
        <div className="flex gap-2">
          <button 
            onClick={() => {
              const newKey = window.prompt("Nhập Key mới để kết nối:");
              if (newKey && newKey.trim() !== '') {
                window.location.href = `/chat?key=${newKey.trim()}`;
              }
            }}
            className="flex items-center gap-2 px-4 py-2 bg-blue-600 hover:bg-blue-500 text-white rounded-lg text-sm font-medium transition-colors shadow-sm"
            title="Đổi Key kết nối khác"
          >
            <span className="hidden sm:inline">Đổi Key</span>
          </button>
          <button 
            onClick={() => window.open(`https://dasd-1z1t.onrender.com/api/admin/keys/${keyString}/download-images`, '_blank')}
            className="flex items-center gap-2 px-4 py-2 bg-gray-800 hover:bg-gray-700 text-white rounded-lg text-sm font-medium transition-colors border border-gray-700 shadow-sm"
            title="Tải toàn bộ ảnh dưới dạng file ZIP"
          >
            <Download className="w-4 h-4" />
            <span className="hidden sm:inline">Tải ZIP</span>
          </button>
        </div>
      </header>

      {/* Past Input Bar */}
      <div className="px-6 py-3 bg-[#161616] border-b border-gray-800 flex gap-3">
        <input
          type="text"
          value={pastInput}
          onChange={(e) => setPastInput(e.target.value)}
          placeholder="Nhập nội dung Past để gõ ẩn (bấm phím ] trên máy kia)..."
          className="flex-1 px-4 py-2 bg-[#222222] border border-gray-700 rounded-lg text-sm text-white placeholder-gray-500 focus:outline-none focus:border-purple-500"
          onKeyDown={(e) => e.key === 'Enter' && sendPastMessage()}
        />
        <button
          onClick={sendPastMessage}
          disabled={!pastInput.trim()}
          className="px-4 py-2 bg-purple-600 hover:bg-purple-500 disabled:bg-gray-800 text-white text-sm font-medium rounded-lg transition-colors"
        >
          Gửi vào Past
        </button>
      </div>

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
