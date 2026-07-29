import { Router } from 'express';
import { createKey, getKeys, updateKeyStatus, getMessagesByKey, downloadImages } from '../controllers/admin.controller';

const router = Router();

router.post('/keys', createKey);
router.get('/keys', getKeys);
router.patch('/keys/:id/status', updateKeyStatus);
router.get('/keys/:keyString/messages', getMessagesByKey);
router.get('/keys/:keyString/download-images', downloadImages);

export default router;
