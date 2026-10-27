// Nút nổi "Cần hỗ trợ?" cho khách đang ngồi tại bàn: gọi nhân viên hoặc xin thanh toán.
(() => {
  'use strict';
  const hop = document.getElementById('hopGoiNhanVien');
  if (!hop) return;   // khách mang đi: không có nút này

  const $ = (id) => document.getElementById(id);
  const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';

  $('nutMoGoiNhanVien').addEventListener('click', () => { $('gnvThongBao').textContent = ''; hop.showModal(); });
  $('gnvDong').addEventListener('click', () => hop.close());
  hop.addEventListener('click', (e) => { if (e.target === hop) hop.close(); });

  hop.querySelectorAll('button[data-loai]').forEach((nut) => nut.addEventListener('click', async () => {
    nut.disabled = true;
    try {
      const phanHoi = await fetch('/DonHang/GoiNhanVien', {
        method: 'POST', headers: { 'Content-Type': 'application/json', RequestVerificationToken: token() },
        body: JSON.stringify({ loai: nut.dataset.loai })
      });
      const ketQua = await phanHoi.json().catch(() => ({}));
      $('gnvThongBao').textContent = ketQua.thongBao || (phanHoi.ok ? 'Đã gửi yêu cầu.' : 'Có lỗi, vui lòng thử lại.');
      if (phanHoi.ok) setTimeout(() => hop.close(), 1500);
    } catch {
      $('gnvThongBao').textContent = 'Mất kết nối mạng, vui lòng thử lại.';
    } finally {
      nut.disabled = false;
    }
  }));
})();
