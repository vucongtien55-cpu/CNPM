// Đối soát cuối ngày: so sánh tiền thực đếm với tiền hệ thống, và tick đối chiếu từng khoản thu.
(() => {
  'use strict';
  const dinhDangTien = (so) => Math.round(so).toString().replace(/\B(?=(\d{3})+(?!\d))/g, '.') + 'đ';

  // ----- Kiểm đếm tiền mặt -----
  const khu = document.getElementById('khuKiemDem');
  if (khu) {
    const tienMatHeThong = +khu.dataset.tienMat;
    const oDauCa = document.getElementById('tienDauCa');
    const oThucDem = document.getElementById('tienThucDem');
    const oKetQua = document.getElementById('ketQuaKiemDem');

    const tinh = () => {
      if (oThucDem.value === '') {
        oKetQua.className = 'ket-qua-kiem-dem full';
        oKetQua.textContent = 'Nhập số tiền thực đếm để so sánh.';
        return;
      }
      const duKien = (+oDauCa.value || 0) + tienMatHeThong;
      const thucDem = +oThucDem.value || 0;
      const chenhLech = thucDem - duKien;
      const cauDuKien = `Trong két phải có ${dinhDangTien(duKien)} (tiền lẻ đầu ca + tiền mặt thu). Bạn đếm được ${dinhDangTien(thucDem)}. `;
      if (chenhLech === 0) {
        oKetQua.className = 'ket-qua-kiem-dem full khop';
        oKetQua.textContent = cauDuKien + 'KHỚP, không chênh lệch.';
      } else if (chenhLech > 0) {
        oKetQua.className = 'ket-qua-kiem-dem full thua';
        oKetQua.textContent = cauDuKien + `THỪA ${dinhDangTien(chenhLech)}. Kiểm tra xem có đơn nào thu hai lần hoặc quên ghi đơn không.`;
      } else {
        oKetQua.className = 'ket-qua-kiem-dem full thieu';
        oKetQua.textContent = cauDuKien + `THIẾU ${dinhDangTien(-chenhLech)}. Kiểm tra đơn chưa thu tiền, tiền thối nhầm, hoặc khoản chi trong ngày.`;
      }
    };
    oDauCa.addEventListener('input', tinh);
    oThucDem.addEventListener('input', tinh);
  }

  // ----- Tick đối chiếu từng khoản đã thu -----
  const cacTick = [...document.querySelectorAll('.tick')];
  const dongTong = document.getElementById('tongTick');
  const capNhatTick = () => {
    let soKhoan = 0, tienMat = 0, chuyenKhoan = 0;
    cacTick.forEach((o) => {
      o.closest('tr').classList.toggle('da-doi-chieu', o.checked);
      if (!o.checked) return;
      soKhoan++;
      if (o.dataset.pt === '1') tienMat += +o.dataset.tien; else chuyenKhoan += +o.dataset.tien;
    });
    if (dongTong) dongTong.textContent = `Đã đối chiếu: ${soKhoan}/${cacTick.length} khoản (tiền mặt ${dinhDangTien(tienMat)}, chuyển khoản ${dinhDangTien(chuyenKhoan)})`;
  };
  cacTick.forEach((o) => o.addEventListener('change', capNhatTick));
  capNhatTick();
})();
