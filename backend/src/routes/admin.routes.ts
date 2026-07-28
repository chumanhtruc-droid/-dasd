import { Router } from 'express';
import { createKey, getKeys, updateKeyStatus, getMessagesByKey } from '../controllers/admin.controller';

const router = Router();

router.post('/keys', createKey);
router.get('/keys', getKeys);
router.patch('/keys/:id/status', updateKeyStatus);
router.get('/keys/:keyString/messages', getMessagesByKey);

export default router;
