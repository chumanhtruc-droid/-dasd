import { Request, Response } from 'express';
import { prisma } from '../config/db';
import archiver from 'archiver';
import axios from 'axios';

export const createKey = async (req: Request, res: Response) => {
  try {
    const { keyString, expiresAt, note, createdBy } = req.body;
    
    const key = await prisma.connectionKey.create({
      data: {
        keyString,
        expiresAt: expiresAt ? new Date(expiresAt) : null,
        note,
        createdBy,
        status: 'ACTIVE'
      }
    });

    res.status(201).json(key);
  } catch (error) {
    res.status(500).json({ error: 'Không thể tạo Key' });
  }
};

export const getKeys = async (req: Request, res: Response) => {
  try {
    const keys = await prisma.connectionKey.findMany({
      orderBy: { createdAt: 'desc' }
    });
    res.json(keys);
  } catch (error) {
    res.status(500).json({ error: 'Lỗi server' });
  }
};

export const updateKeyStatus = async (req: Request, res: Response) => {
  try {
    const { id } = req.params;
    const { status } = req.body; // ACTIVE, LOCKED, EXPIRED

    const key = await prisma.connectionKey.update({
      where: { id },
      data: { status }
    });

    res.json(key);
  } catch (error) {
    res.status(500).json({ error: 'Không thể cập nhật Key' });
  }
};

export const getMessagesByKey = async (req: Request, res: Response) => {
  try {
    const { keyString } = req.params;
    
    const key = await prisma.connectionKey.findUnique({
      where: { keyString }
    });

    if (!key) return res.status(404).json({ error: 'Key not found' });

    const messages = await prisma.message.findMany({
      where: { keyId: key.id },
      orderBy: { createdAt: 'asc' }
    });

    res.json(messages);
  } catch (error) {
    res.status(500).json({ error: 'Lỗi server' });
  }
};

export const downloadImages = async (req: Request, res: Response) => {
  try {
    const { keyString } = req.params;
    
    const key = await prisma.connectionKey.findUnique({
      where: { keyString }
    });

    if (!key) return res.status(404).json({ error: 'Key not found' });

    // Lấy tất cả các tin nhắn loại IMAGE do Windows (USER1) gửi
    const images = await prisma.message.findMany({
      where: { 
        keyId: key.id,
        type: 'IMAGE',
        // sender: 'USER1' // Bỏ comment nếu chỉ muốn lấy ảnh từ app
      },
      orderBy: { createdAt: 'asc' }
    });

    if (images.length === 0) {
      return res.status(404).json({ error: 'No images found' });
    }

    res.attachment(`${keyString}-images.zip`);
    const archive = archiver('zip', { zlib: { level: 9 } });

    archive.on('error', (err) => {
      res.status(500).send({ error: err.message });
    });

    archive.pipe(res);

    let count = 1;
    for (const img of images) {
      try {
        const response = await axios.get(img.content, { responseType: 'stream' });
        archive.append(response.data, { name: `image-${count}.jpg` });
        count++;
      } catch (err) {
        console.error('Failed to download image:', img.content);
      }
    }

    await archive.finalize();
  } catch (error) {
    res.status(500).json({ error: 'Lỗi server' });
  }
};
