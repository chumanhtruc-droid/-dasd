import { Server, Socket } from 'socket.io';
import { prisma } from '../config/db';

// keyId -> Set of sockets
const roomUsers = new Map<string, Set<string>>();

export const setupSocket = (io: Server) => {
  io.on('connection', (socket: Socket) => {
    console.log(`User connected: ${socket.id}`);

    // Tham gia phòng bằng Key
    socket.on('join_room', async (data: { keyString: string; sender: string }) => {
      try {
        const key = await prisma.connectionKey.findUnique({
          where: { keyString: data.keyString }
        });

        if (!key) {
          socket.emit('error', { message: 'Key không tồn tại' });
          return;
        }

        if (key.status !== 'ACTIVE') {
          socket.emit('error', { message: 'Key đã bị khóa hoặc hết hạn' });
          return;
        }

        if (key.expiresAt && key.expiresAt < new Date()) {
          socket.emit('error', { message: 'Key đã hết hạn' });
          return;
        }

        socket.join(key.id);
        socket.data.keyId = key.id;
        socket.data.sender = data.sender; // 'USER1' (Windows) hoặc 'USER2' (Web)
        
        let users = roomUsers.get(key.id) || new Set();
        users.add(socket.id);
        roomUsers.set(key.id, users);

        socket.emit('joined', { message: 'Đã tham gia phòng', keyId: key.id });
        
        // Notify others
        socket.to(key.id).emit('user_status', { sender: data.sender, status: 'ONLINE' });

      } catch (error) {
        console.error(error);
        socket.emit('error', { message: 'Lỗi server' });
      }
    });

    // Nhận và phát lại tin nhắn
    socket.on('send_message', async (data: { content: string, type: string }) => {
      const { keyId, sender } = socket.data;
      if (!keyId || !sender) return;

      const message = await prisma.message.create({
        data: {
          keyId: keyId,
          sender: sender,
          type: data.type || 'TEXT',
          content: data.content,
        }
      });

      // Phát cho mọi người trong phòng (bao gồm cả người gửi để xác nhận Đã Gửi)
      io.to(keyId).emit('new_message', message);
    });

    // Đánh dấu đã xem
    socket.on('mark_read', async (data: { messageIds: string[] }) => {
      const { keyId } = socket.data;
      if (!keyId) return;

      await prisma.message.updateMany({
        where: { id: { in: data.messageIds } },
        data: { status: 'READ' }
      });

      io.to(keyId).emit('messages_read', data.messageIds);
    });

    socket.on('disconnect', () => {
      const { keyId, sender } = socket.data;
      if (keyId) {
        let users = roomUsers.get(keyId);
        if (users) {
          users.delete(socket.id);
          if (users.size === 0) {
            roomUsers.delete(keyId);
          }
        }
        socket.to(keyId).emit('user_status', { sender: sender, status: 'OFFLINE' });
      }
      console.log(`User disconnected: ${socket.id}`);
    });
  });
};
