import express from 'express';
import http from 'http';
import { Server } from 'socket.io';
import cors from 'cors';
import dotenv from 'dotenv';
import multer from 'multer';
import { connectDB } from './config/db';
import { v2 as cloudinary } from 'cloudinary';
import adminRoutes from './routes/admin.routes';
import { setupSocket } from './sockets/chat.socket';
import { v4 as uuidv4 } from 'uuid';

dotenv.config();

const app = express();
const server = http.createServer(app);
const io = new Server(server, {
  cors: {
    origin: '*',
    methods: ['GET', 'POST']
  }
});

// Middleware
app.use(cors());
app.use(express.json());

// Init services
connectDB();
setupSocket(io);

// Cloudinary config
cloudinary.config({
  cloud_name: process.env.CLOUDINARY_CLOUD_NAME || "qncudzpu",
  api_key: process.env.CLOUDINARY_API_KEY || "498533888516325",
  api_secret: process.env.CLOUDINARY_API_SECRET || "tnjs2lbGrew86ayDYwK9bmNrpjl",
});

// Routes
app.use('/api/admin', adminRoutes);

// Upload endpoint
const upload = multer({ storage: multer.memoryStorage() });
app.post('/api/upload', upload.single('file'), async (req, res) => {
  if (!req.file) {
    return res.status(400).json({ error: 'No file uploaded' });
  }

  try {
    const fileName = `${uuidv4()}-${req.file.originalname}`;
    
    // Upload via stream
    const uploadStream = cloudinary.uploader.upload_stream(
      { resource_type: 'image', public_id: fileName },
      (error, result) => {
        if (error || !result) {
          console.error('Cloudinary upload error:', error);
          return res.status(500).json({ error: 'Upload failed: ' + (error?.message || JSON.stringify(error)) });
        }
        res.json({ url: result.secure_url });
      }
    );

    // End stream with buffer
    uploadStream.end(req.file.buffer);
  } catch (error) {
    console.error('Upload error:', error);
    res.status(500).json({ error: 'Upload failed' });
  }
});

const PORT = process.env.PORT || 5000;
server.listen(PORT, () => {
  console.log(`Server is running on port ${PORT}`);
});
